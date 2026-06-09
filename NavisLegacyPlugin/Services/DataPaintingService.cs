using System;
using System.Collections.Generic;
using System.Data;
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
			DataTable table,
			Dictionary<string, string> columnMap,
			string lookupTab,
			string lookupProperty,
			string matchColumn,
			bool writeToLeafItems,
			Action<string> reportText,
			Action<int> reportPercent)
		{
			int matched = 0;
			int unmatched = 0;

			var lookup = await _lookupService.GetOrBuildLookupAsync(
				lookupTab,
				lookupProperty,
				new Progress<ModelLookupService.LookupProgressInfo>(info =>
				{
					reportText?.Invoke($"{info.Stage} {info.ItemsScanned}");
				}));

			reportPercent?.Invoke(35);

			int rowIndex = 0;
			int total = table.Rows.Count;

			var itemWriteMap =
				new Dictionary<ModelItem, Dictionary<string, Dictionary<string, string>>>();

			foreach (DataRow row in table.Rows)
			{
				rowIndex++;

				var mapped = PropertyMappingHelper.MapRow(row, columnMap);
				var instruction = PaintInstructionBuilder.Build(mapped, matchColumn);

				if (instruction == null || string.IsNullOrWhiteSpace(instruction.MatchValue))
					continue;

				if (!lookup.TryGetValue(instruction.MatchValue, out var item))
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
					reportPercent?.Invoke(35 + (int)(35.0 * rowIndex / total));
					reportText?.Invoke($"Preparing row {rowIndex} of {total}...");

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
					reportPercent?.Invoke(70 + (int)(30.0 * itemIndex / Math.Max(itemTotal, 1)));
					reportText?.Invoke($"Writing item {itemIndex} of {itemTotal}...");

					await System.Windows.Application.Current.Dispatcher.InvokeAsync(
						() => { },
						System.Windows.Threading.DispatcherPriority.Background);
				}

				if (writeToLeafItems)
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
}
