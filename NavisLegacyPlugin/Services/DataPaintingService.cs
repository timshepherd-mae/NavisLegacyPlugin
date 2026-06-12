using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Autodesk.Navisworks.Api;
using NavisLegacyPlugin.Helpers;
using NavisLegacyPlugin.Models;

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

			var itemWriteMap =
				new Dictionary<ModelItem, Dictionary<string, Dictionary<string, string>>>();

			foreach (DataRow row in table.Rows)
			{
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
			}

			foreach (var entry in itemWriteMap)
			{
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
			}

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

			int matched = 0;
			int unmatched = 0;

			var table = await dataSource.GetDataAsync(progress.ProgressText);

			var itemWriteMap =
				new Dictionary<ModelItem, Dictionary<string, Dictionary<string, string>>>();

			foreach (DataRow row in table.Rows)
			{
				var mapped = PropertyMappingHelper.MapRow(row, mapping.ColumnMap);
				var instruction = PaintInstructionBuilder.Build(mapped, mapping.MatchColumn);

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
						propDict[kvp.Key] = kvp.Value;
				}
			}

			foreach (var entry in itemWriteMap)
			{
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
				CollectLeafItems(child, results);
		}
	}
}
