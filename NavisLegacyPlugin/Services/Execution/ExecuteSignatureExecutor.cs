using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Autodesk.Navisworks.Api;
using NavisLegacyPlugin.Models;

namespace NavisLegacyPlugin.Services.Execution
{
    public sealed class ExecuteSignatureExecutor
    {
        private readonly ComPropertyWriteService _writer;

        public ExecuteSignatureExecutor(ComPropertyWriteService writer)
        {
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        }

        public async Task<ExecuteResult> ExecuteAsync(
            ExecuteSignature signature)
        {

            if (signature == null)
                throw new ArgumentNullException(nameof(signature));

            if (signature.DataSource == null)
                throw new ArgumentException(
                    "DataSource is required.");

            if (signature.MappingStrategy == null)
                throw new ArgumentException(
                    "MappingStrategy is required.");

            if (signature.LookupProvider == null)
                throw new ArgumentException(
                    "LookupProvider is required.");

            Debug.WriteLine(">>> USING EXECUTESIGNATURE PATH <<<");

            var lookupDict = await signature.LookupProvider.BuildLookupAsync(signature.ProgressConfig);

            int matched = 0;
            int unmatched = 0;
            int written = 0;
            int skipped = 0;

            var table = await signature.DataSource.GetDataAsync(signature.ProgressConfig.ProgressText);

            var itemWriteMap =
                new Dictionary<ModelItem, Dictionary<string, Dictionary<string, string>>>();

            int totalRows = table.Rows.Count;
            int rowIndex = 0;

            signature.ProgressConfig.ProgressText?.Report("Grouping data...");

            var mappingStrategy = signature.MappingStrategy;

            foreach (DataRow row in table.Rows)
            {
                rowIndex++;

                var instruction = mappingStrategy.Map(row);

                if (instruction == null || string.IsNullOrWhiteSpace(instruction.MatchValue))
                    continue;


                if (!lookupDict.TryGetValue(instruction.MatchValue, out var item))
                {
                    unmatched++;
                    continue;
                }

                matched++;

                foreach (var tab in instruction.PropertiesByTab)
                {
                    if (!itemWriteMap.TryGetValue(item, out var tabDict))
                    {
                        tabDict = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
                        itemWriteMap[item] = tabDict;
                    }

                    if (!tabDict.TryGetValue(tab.Key, out var propDict))
                    {
                        propDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        tabDict[tab.Key] = propDict;
                    }

                    foreach (var kvp in tab.Value)
                        propDict[kvp.Key] = kvp.Value;
                }

                if (rowIndex % 50 == 0) // throttle
                {
                    int percent = 35 + (rowIndex * 30 / totalRows);
                    signature.ProgressConfig.ProgressPercent?.Report(percent);
                    signature.ProgressConfig.ProgressText?.Report($"Grouping {rowIndex}/{totalRows}");

                    await System.Windows.Application.Current.Dispatcher
                        .InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Background);
                }

            }

            int totalItems = itemWriteMap.Count;
            int writeIndex = 0;

            signature.ProgressConfig.ProgressText?.Report("Writing data...");

            foreach (var entry in itemWriteMap)
            {
                writeIndex++;

                var targetItems = new List<ModelItem>();

                if (signature.WriteConfig.WriteToLeafItems)
                {
                    CollectLeafItems(
                        entry.Key,
                        targetItems);
                }
                else
                {
                    targetItems.Add(entry.Key);
                }

                foreach (var targetItem in targetItems)
                {
                    foreach (var tab in entry.Value)
                    {
                        foreach (var prop in tab.Value)
                        {
                            var categoryName = tab.Key;
                            var propName = prop.Key;
                            var propValue = prop.Value;

                            var category =
                                targetItem.PropertyCategories
                                    .FindCategoryByDisplayName(
                                        categoryName);

                            var existingProp =
                                category?
                                    .Properties
                                    .FindPropertyByDisplayName(
                                        propName);

                            if (!signature.WriteConfig.Overwrite)
                            {
                                if (existingProp != null)
                                {
                                    skipped++;
                                    continue;
                                }
                            }

                            _writer.WriteUserDefinedProperties(
                                targetItem,
                                categoryName,
                                new Dictionary<string, string>
                                {
                        { propName, propValue }
                                });

                            written++;
                        }
                    }
                }

                if (writeIndex % 10 == 0)
                {
                    int percent =
                        65 + (writeIndex * 35 / totalItems);

                    signature.ProgressConfig
                        .ProgressPercent?
                        .Report(percent);

                    signature.ProgressConfig
                        .ProgressText?
                        .Report(
                            $"Writing {writeIndex}/{totalItems}");

                    await System.Windows.Application.Current
                        .Dispatcher.InvokeAsync(
                            () => { },
                            System.Windows.Threading
                                .DispatcherPriority.Background);
                }
            }

            signature.ProgressConfig.ProgressPercent?.Report(100);
            signature.ProgressConfig.ProgressText?.Report("Complete.");

            return new ExecuteResult(
                matched,
                unmatched,
                written,
                skipped);
        }

        private void CollectLeafItems(ModelItem item, List<ModelItem> results)
        {
            if (item == null) return;

            if (item.Children == null || !item.Children.Any())
            {
                if (!results.Contains(item))
                    results.Add(item);
                return;
            }

            foreach (ModelItem child in item.Children)
                CollectLeafItems(child, results);
        }


    }
}
