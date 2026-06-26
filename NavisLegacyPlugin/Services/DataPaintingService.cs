using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Autodesk.Navisworks.Api;
using NavisLegacyPlugin.Helpers;
using NavisLegacyPlugin.Models;
using NavisLegacyPlugin.Services.Lookups;
using NavisLegacyPlugin.Services.Mappers;

namespace NavisLegacyPlugin.Services
{
	public class DataPaintingService
	{
		private readonly ModelLookupService _lookupService;
		private readonly ComPropertyWriteService _writer;

		public DataPaintingService(
			ModelLookupService lookupService,
			ComPropertyWriteService writer)
		{
			_lookupService = lookupService;
			_writer = writer;
		}

		public async Task<(int matched, int unmatched)> ExecuteAsync(
			IDataSource dataSource,
			MappingConfig mapping,
			LookupConfig lookup,
			WriteConfig writeConfig,
			ProgressConfig progress)
		{
			System.Diagnostics.Debug.WriteLine(">>> USING SYNCHRO LOOKUP PATH <<<");

			int matched = 0;
			int unmatched = 0;

			var lookupDict = await _lookupService.GetOrBuildLookupAsync(
				lookup.LookupTab,
				lookup.LookupProperty,
				new Progress<ModelLookupService.LookupProgressInfo>(info =>
				{
					progress.ProgressText?.Report($"{info.Stage} {info.ItemsScanned}");
				}));

			progress.ProgressPercent?.Report(35);
			progress.ProgressText?.Report("Reading data...");

			var table = await dataSource.GetDataAsync(progress.ProgressText);

			progress.ProgressText?.Report("Processing data...");
			progress.ProgressPercent?.Report(35);

			var itemWriteMap =
				new Dictionary<ModelItem, Dictionary<string, Dictionary<string, string>>>();

			int totalRows = table.Rows.Count;
			int rowIndex = 0;

			progress.ProgressText?.Report("Grouping data...");

			foreach (DataRow row in table.Rows)
			{
				rowIndex++;

				var mapped = PropertyMappingHelper.MapRow(row, mapping.ColumnMap);
				var instruction = PaintInstructionBuilder.Build(mapped, mapping.MatchColumn);

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

				if (rowIndex % 50 == 0) // lower than 50 for responsiveness
				{
					int percent = 35 + (rowIndex * 30 / totalRows);
					progress.ProgressPercent?.Report(percent);
					progress.ProgressText?.Report($"Grouping {rowIndex}/{totalRows}");

					await System.Windows.Application.Current.Dispatcher
						.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Background);
				}

			}

			int totalItems = itemWriteMap.Count;
			int writeIndex = 0;

			progress.ProgressText?.Report("Writing data...");

			foreach (var entry in itemWriteMap)
			{
				writeIndex++;

				if (writeConfig.WriteToLeafItems)
				{
					var leafItems = new List<ModelItem>();
					CollectLeafItems(entry.Key, leafItems);

					foreach (var leaf in leafItems)
						foreach (var tab in entry.Value)
							_writer.WriteUserDefinedProperties(leaf, tab.Key, tab.Value);
				}
				else
				{
					foreach (var tab in entry.Value)
						_writer.WriteUserDefinedProperties(entry.Key, tab.Key, tab.Value);
				}

				if (writeIndex % 200 == 0)
				{
					int percent = 65 + (writeIndex * 35 / totalItems);
					progress.ProgressPercent?.Report(percent);
					progress.ProgressText?.Report($"Writing {writeIndex}/{totalItems}");

					await System.Windows.Application.Current.Dispatcher
						.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Background);

				}
			}

			progress.ProgressPercent?.Report(100);
			progress.ProgressText?.Report("Complete.");

			return (matched, unmatched);
		}

		public async Task<(int matched, int unmatched)> ExecuteAsync(
			IDataSource dataSource,
			MappingConfig mapping,
			Dictionary<string, ModelItem> lookup,
			WriteConfig writeConfig,
			ProgressConfig progress)
		{
			System.Diagnostics.Debug.WriteLine(">>> USING GUID LOOKUP PATH <<<");

			// wrap existing lookup in provider
			var lookupProvider = new DictionaryLookupProvider(lookup);

			// build lookup through provider (no behaviour change)
			var lookupDict = await lookupProvider.BuildLookupAsync(progress);

			// ==========================
			// DEBUG: confirm lookup size
			//System.Diagnostics.Debug.WriteLine($"[STEP A] Lookup count: {lookupDict.Count}");
			// ==========================

			int matched = 0;
			int unmatched = 0;
			int written = 0;
			int skipped = 0;

			var table = await dataSource.GetDataAsync(progress.ProgressText);

			var itemWriteMap =
				new Dictionary<ModelItem, Dictionary<string, Dictionary<string, string>>>();

			int totalRows = table.Rows.Count;
			int rowIndex = 0;

			progress.ProgressText?.Report("Grouping data...");

			var mappingStrategy = new MappingConfigStrategy(mapping);

			// ==========================
			// DEBUG
			System.Diagnostics.Debug.WriteLine("[STEP B] MappingStrategy initialised");
			// ==========================

			foreach (DataRow row in table.Rows)
			{
				rowIndex++;
				
				var instruction = mappingStrategy.Map(row);

				// ==========================
				// DEBUG
				//System.Diagnostics.Debug.WriteLine($"[STEP A] MatchValue: {instruction?.MatchValue}");
				System.Diagnostics.Debug.WriteLine($"[STEP B] MatchValue: {instruction?.MatchValue}");
				if (instruction == null)
				{
					System.Diagnostics.Debug.WriteLine("[STEP B] NULL instruction");
				}
				foreach (var tab in instruction.PropertiesByTab)
				{
					foreach (var prop in tab.Value)
					{
						System.Diagnostics.Debug.WriteLine(
							$"[STEP B] WRITE VALUE → {tab.Key}.{prop.Key} = '{prop.Value}'");
					}
				}
				// ==========================

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
					progress.ProgressPercent?.Report(percent);
					progress.ProgressText?.Report($"Grouping {rowIndex}/{totalRows}");

					await System.Windows.Application.Current.Dispatcher
						.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Background);
				}

			}

			int totalItems = itemWriteMap.Count;
			int writeIndex = 0;

			progress.ProgressText?.Report("Writing data...");

			foreach (var entry in itemWriteMap)
			{
				writeIndex++;
				
				//if (writeConfig.WriteToLeafItems)
				//{
				//	var leafItems = new List<ModelItem>();
				//	CollectLeafItems(entry.Key, leafItems);

				//	foreach (var leaf in leafItems)
				//		foreach (var tab in entry.Value)
				//			_writer.WriteUserDefinedProperties(leaf, tab.Key, tab.Value);
				//}
				//else
				//{
				//	foreach (var tab in entry.Value)
				//		_writer.WriteUserDefinedProperties(entry.Key, tab.Key, tab.Value);
				//}

				foreach (var tab in entry.Value)
				{
					foreach (var prop in tab.Value)
					{
						var categoryName = tab.Key;   // "MAE-4D"
						var propName = prop.Key;      // "RID"
						var propValue = prop.Value;

						var category = entry.Key.PropertyCategories
							.FindCategoryByDisplayName(categoryName);

						var existingProp = category?
							.Properties
							.FindPropertyByDisplayName(propName);

						if (existingProp != null)
						{
							var existingValue = existingProp.Value?.ToDisplayString();

							if (!writeConfig.Overwrite &&
								!string.IsNullOrWhiteSpace(existingValue))
							{
								skipped++;
								continue;
							}
						}

						_writer.WriteUserDefinedProperties(
							entry.Key,
							categoryName,
							new Dictionary<string, string>
							{
				{ propName, propValue }
							});

						written++;
					}
				}


				if (writeIndex % 10 == 0)
				{
					int percent = 65 + (writeIndex * 35 / totalItems);
					progress.ProgressPercent?.Report(percent);
					progress.ProgressText?.Report($"Writing {writeIndex}/{totalItems}");

					await System.Windows.Application.Current.Dispatcher
						.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Background);
				}

			}

			progress.ProgressPercent?.Report(100);
			progress.ProgressText?.Report("Complete.");

			// ==========================
			// DEBUG
			System.Diagnostics.Debug.WriteLine($"[STEP A] RESULT → Matched: {matched}, Unmatched: {unmatched}");

			System.Diagnostics.Debug.WriteLine($"[STEP B] RESULT → Written: {written}, Skipped: {skipped}");

			// ==========================

			return (matched, unmatched);
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
