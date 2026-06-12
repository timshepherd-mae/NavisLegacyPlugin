using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Autodesk.Navisworks.Api;
using NavisLegacyPlugin.Helpers;

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

			int rowIndex = 0;
			int total = table.Rows.Count;

			var itemWriteMap =
				new Dictionary<ModelItem, Dictionary<string, Dictionary<string, string>>>();

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
					{
						propDict[kvp.Key] = kvp.Value;
					}
				}

				if (rowIndex % 25 == 0)
				{
					progress.ProgressPercent?.Report(35 + (int)(35.0 * rowIndex / total));
					progress.ProgressText?.Report($"Preparing row {rowIndex} of {total}...");

					await System.Windows.Application.Current.Dispatcher.InvokeAsync(
						() => { },
						System.Windows.Threading.DispatcherPriority.Background);
				}
			}

			int itemIndex = 0;
			int itemTotal = itemWriteMap.Count;

			foreach (var entry in itemWriteMap)
			{
				var item = entry.Key;
				itemIndex++;

				// ✅ PROGRESS BEFORE WRITE
				if (itemIndex % 5 == 0 || itemIndex == itemTotal)
				{
					progress.ProgressText?.Report($"Writing item {itemIndex} of {itemTotal}...");
					progress.ProgressPercent?.Report(70 + (int)(30.0 * itemIndex / Math.Max(itemTotal, 1)));

					await System.Windows.Application.Current.Dispatcher.InvokeAsync(
						() => { },
						System.Windows.Threading.DispatcherPriority.Background);
				}

				if (writeConfig.WriteToLeafItems)
				{
					var leafItems = new List<ModelItem>();
					CollectLeafItems(item, leafItems);

					int leafIndex = 0;
					int leafTotal = leafItems.Count;

					foreach (var leaf in leafItems)
					{

						foreach (var tab in entry.Value)
						{
							_writer.WriteUserDefinedProperties(leaf, tab.Key, tab.Value);
						}

						leafIndex++;
					}
				}
				else
				{
					foreach (var tab in entry.Value)
					{
						_writer.WriteUserDefinedProperties(item, tab.Key, tab.Value);
					}
				}
			}


			return (matched, unmatched);
		}

		public async Task<(int matched, int unmatched)> ExecuteAsync(
			IDataSource dataSource,
			MappingConfig mapping,
			Dictionary<string, ModelItem> lookupDict,
			WriteConfig writeConfig,
			ProgressConfig progress)
		{
			System.Diagnostics.Debug.WriteLine(">>> USING GUID LOOKUP PATH <<<");

			int matched = 0;
			int unmatched = 0;

			progress.ProgressText?.Report("Reading data...");

			var table = await dataSource.GetDataAsync(progress.ProgressText);

			int rowIndex = 0;
			int total = table.Rows.Count;

			Debug.WriteLine($"TABLE ROWS INSIDE SERVICE: {total}");

			var itemWriteMap =
				new Dictionary<ModelItem, Dictionary<string, Dictionary<string, string>>>();

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
						tabDict = new Dictionary<string, Dictionary<string, string>>();
						itemWriteMap[item] = tabDict;
					}

					if (!tabDict.TryGetValue(tab.Key, out var propDict))
					{
						propDict = new Dictionary<string, string>();
						tabDict[tab.Key] = propDict;
					}

					foreach (var prop in tab.Value)
					{
						propDict[prop.Key] = prop.Value;
					}
				}

				if (rowIndex % 50 == 0)
				{
					int percent = (int)((double)rowIndex / total * 100);
					progress.ProgressPercent?.Report(percent);
				}
			}

			progress.ProgressText?.Report("Writing properties...");

			foreach (var kvp in itemWriteMap)
			{
				var item = kvp.Key;
				var tabDict = kvp.Value;

				foreach (var tab in tabDict)
				{
					_writer.WriteToModelItem(
						item,
						tab.Key,
						tab.Value,
						writeConfig.WriteToLeafItems);
				}
			}

			progress.ProgressPercent?.Report(100);

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
			{
				CollectLeafItems(child, results);
			}
		}
	}

	public class MappingConfig
	{
		public Dictionary<string, string> ColumnMap { get; set; }
		public string MatchColumn { get; set; }
	}

	public class LookupConfig
	{
		public string LookupTab { get; set; }
		public string LookupProperty { get; set; }
	}

	public class WriteConfig
	{
		public bool WriteToLeafItems { get; set; }
	}

	public class ProgressConfig
	{
		public IProgress<string> ProgressText { get; set; }
		public IProgress<int> ProgressPercent { get; set; }
	}

}
