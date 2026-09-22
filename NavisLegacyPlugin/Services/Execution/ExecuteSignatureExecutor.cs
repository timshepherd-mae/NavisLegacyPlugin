using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Autodesk.Navisworks.Api;
using NavisLegacyPlugin.Models;
using NavisLegacyPlugin.Models.Scopes;
using NavisLegacyPlugin.Services.Collections;
using NavisLegacyPlugin.Services.SelectionSets;

namespace NavisLegacyPlugin.Services.Execution
{
    public sealed class ExecuteSignatureExecutor
    {
        private readonly ComPropertyWriteService _writer;
        private readonly IDataTransferHierarchyValidator _hierarchyValidator;
        private readonly ITransferScopeResolver _scopeResolver;
        private readonly ITransferScopeCollectionResolver _scopeCollectionResolver;

        public ExecuteSignatureExecutor(ComPropertyWriteService writer)
            : this(
                writer,
                new DataTransferHierarchyValidator(
                    new NavisSelectionSetProvider()),
                new NavisScopeResolver(),
                new TransferScopeCollectionResolver())
        {
        }

        public ExecuteSignatureExecutor(
            ComPropertyWriteService writer,
            IDataTransferHierarchyValidator hierarchyValidator,
            ITransferScopeResolver scopeResolver,
            ITransferScopeCollectionResolver scopeCollectionResolver)
        {
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));
            if (hierarchyValidator == null)
                throw new ArgumentNullException(nameof(hierarchyValidator));
            if (scopeResolver == null)
                throw new ArgumentNullException(nameof(scopeResolver));
            if (scopeCollectionResolver == null)
                throw new ArgumentNullException(nameof(scopeCollectionResolver));
            _writer = writer;
            _hierarchyValidator = hierarchyValidator;
            _scopeResolver = scopeResolver;
            _scopeCollectionResolver = scopeCollectionResolver;
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

            ScopeResolution sourceScope = null;
            ScopeResolution targetScope = null;

            if (signature.SelectionSets != null)
            {
                Document document = Application.ActiveDocument;
                ValidateRequiredScope(document, DataTransferSelectionSetNames.Source);
                ValidateRequiredScope(document, DataTransferSelectionSetNames.Target);

                sourceScope = _scopeResolver.Resolve(
                    document,
                    new ScopeDefinition(
                        TransferScopeType.Source,
                        new CurrentDocumentLocation(),
                        signature.SelectionSets.SourcePath));

                targetScope = _scopeResolver.Resolve(
                    document,
                    new ScopeDefinition(
                        TransferScopeType.Target,
                        new CurrentDocumentLocation(),
                        signature.SelectionSets.TargetPath));

                Debug.WriteLine("Resolved SOURCE items: " + sourceScope.ItemCount);
                Debug.WriteLine("Resolved TARGET items: " + targetScope.ItemCount);
            }

            var lookupDict = await signature.LookupProvider.BuildLookupAsync(signature.ProgressConfig);

            IEnumerable<ModelItem> sourceItems = GetScopeItems(
                sourceScope,
                signature.SelectionSets == null
                    ? null
                    : signature.SelectionSets.SourceResolutionType);


            IEnumerable<ModelItem> targetItems = GetScopeItems(
                targetScope,
                signature.SelectionSets == null
                    ? null
                    : signature.SelectionSets.TargetResolutionType);


            HashSet<Guid> sourceBoundary = sourceItems == null
                ? null
                : new HashSet<Guid>(
                    sourceItems.Select(item => item.InstanceGuid));

            HashSet<Guid> targetBoundary = targetItems == null
                ? null
                : new HashSet<Guid>(
                    targetItems.Select(item => item.InstanceGuid));

            if (sourceBoundary != null)
            {
                lookupDict = lookupDict
                    .Where(pair =>
                        pair.Value != null &&
                        sourceBoundary.Contains(pair.Value.InstanceGuid))
                    .ToDictionary(
                        pair => pair.Key,
                        pair => pair.Value,
                        StringComparer.OrdinalIgnoreCase);
            }

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

                if (targetBoundary != null &&
                    !targetBoundary.Contains(item.InstanceGuid))
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

                var writeTargetItems = new List<ModelItem>();

                if (signature.WriteConfig.WriteToLeafItems)
                {
                    IModelItemCollectionResolver writeResolver =
                        new ModelItemCollectionResolver();
                    Models.Collections.CollectionResolutionResult writeResult =
                        writeResolver.Resolve(new[] { entry.Key });

                    writeTargetItems.AddRange(
                        writeResult.GetItems(
                            Models.Collections.CollectionResolutionType.Leaf));
                }
                else
                {
                    // Legacy compatibility: false means the matched item only.
                    writeTargetItems.Add(entry.Key);
                }

                foreach (var targetItem in writeTargetItems)
                {
                    if (targetBoundary != null &&
                        !targetBoundary.Contains(targetItem.InstanceGuid))
                    {
                        skipped++;
                        continue;
                    }

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


        private IEnumerable<ModelItem> GetScopeItems(
            ScopeResolution scopeResolution,
            Models.Collections.CollectionResolutionType? resolutionType)
        {
            if (scopeResolution == null)
            {
                Debug.WriteLine("GetScopeItems: scopeResolution is null.");
                return null;
            }

            Debug.WriteLine(
                "GetScopeItems: scope=" + scopeResolution.Definition.ScopeType +
                " | explicit roots=" + scopeResolution.Items.Count +
                " | requested type=" +
                (resolutionType.HasValue
                    ? resolutionType.Value.ToString()
                    : "<null: legacy direct-items path>"));

            if (!resolutionType.HasValue)
            {
                Debug.WriteLine(
                    "GetScopeItems: returning unexpanded ScopeResolution.Items. " +
                    "Count=" + scopeResolution.Items.Count);
                return scopeResolution.Items;
            }

            IReadOnlyCollection<ModelItem> resolvedItems =
                _scopeCollectionResolver.Resolve(
                    scopeResolution,
                    resolutionType.Value);

            Debug.WriteLine(
                "GetScopeItems: returning recursively resolved " +
                resolutionType.Value +
                " collection. Count=" + resolvedItems.Count);

            return resolvedItems;
        }

        private void ValidateRequiredScope(
            Document document,
            string scopeName)
        {
            DataTransferHierarchyValidationResult result =
                _hierarchyValidator.ValidateScope(document, scopeName);

            if (!result.IsValid)
                throw new InvalidOperationException(result.ToDisplayMessage());
        }

    }
}


