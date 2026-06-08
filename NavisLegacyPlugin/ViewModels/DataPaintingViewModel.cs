using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows.Input;
using Autodesk.Navisworks.Api;
using NavisLegacyPlugin.Helpers;
using NavisLegacyPlugin.Services;
namespace NavisLegacyPlugin.ViewModels
{
	public class DataPaintingViewModel : ViewModelBase
	{
		private readonly ComPropertyWriteService _writer;
		private readonly CsvDataService _csvService = new CsvDataService();
		private readonly ModelLookupService _modelLookupService = new ModelLookupService();
		public ICommand WriteTestCommand { get; }
		public ICommand GetDataCommand { get; }

		private string _status = "Ready.";
		public string Status
		{
			get => _status;
			private set { _status = value; OnPropertyChanged(); }
		}

		private int _progressPercent;
		public int ProgressPercent
		{
			get => _progressPercent;
			set { _progressPercent = value; OnPropertyChanged(); }
		}

		private string _progressText = "";
		public string ProgressText
		{
			get => _progressText;
			set { _progressText = value; OnPropertyChanged(); }
		}

		private bool _isBusy;
		public bool IsBusy
		{
			get => _isBusy;
			set { _isBusy = value; OnPropertyChanged(); }
		}

		public DataPaintingViewModel(ComPropertyWriteService writer)
		{
			_writer = writer;

			WriteTestCommand = new RelayCommand(WriteTest);
			GetDataCommand = new RelayCommand(GetData);
		}

		private void WriteTest()
		{
			var props = new Dictionary<string, string>
		{
			{ "RID", DateTime.Now.ToString("HHmmss") }
		};

			_writer.WriteToCurrentSelection("Synchro", props, false);
		}

		private async void GetData()
		{
			try
			{
				IsBusy = true;
				ProgressText = "Reading CSV...";

				await System.Windows.Application.Current.Dispatcher.InvokeAsync(
					() => { },
					System.Windows.Threading.DispatcherPriority.Background);

				var columnMap = new Dictionary<string, string>
			{
				{ "3DUF:Synchro_SynchroID", "Synchro.SynchroID" },
				{ "3DUF:RID", "MAE-4D.RID" }
			};

				var csvProgress = new Progress<int>(rows =>
				{
					ProgressText = $"Reading CSV... {rows}";
				});

				var table = _csvService.ReadCsvWithProgress(
					@"C:\Users\tshepherd\OneDrive - Murphy\_dev\Synchro\ResourceTransfer\StFergus Unit Test\Tranfer Process Test\data.csv",
					2,
					"3DUF:RID",
					csvProgress);

				ProgressPercent = 15;

				var lookupProgress = new Progress<ModelLookupService.LookupProgressInfo>(info =>
				{
					ProgressText = $"{info.Stage} {info.ItemsScanned}";
				});

				var lookup = await _modelLookupService.GetOrBuildLookupAsync(
					"Synchro",
					"SynchroID",
					lookupProgress);

				ProgressPercent = 35;

				await System.Windows.Application.Current.Dispatcher.InvokeAsync(
					() => { },
					System.Windows.Threading.DispatcherPriority.Background);

				int rowIndex = 0;
				int matched = 0;
				int unmatched = 0;
				int total = table.Rows.Count;

				// ✅ NEW: group by ModelItem
				var itemWriteMap =
					new Dictionary<ModelItem, Dictionary<string, Dictionary<string, string>>>();

				foreach (DataRow row in table.Rows)
				{
					rowIndex++;

					var mapped = PropertyMappingHelper.MapRow(row, columnMap);
					var instruction = PaintInstructionBuilder.Build(mapped, "Synchro.SynchroID");

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
						ProgressPercent = 35 + (int)(35.0 * rowIndex / total);
						ProgressText = $"Preparing row {rowIndex} of {total}...";

						await System.Windows.Application.Current.Dispatcher.InvokeAsync(
							() => { },
							System.Windows.Threading.DispatcherPriority.Background);
					}
				}

				// ✅ WRITE PHASE (unchanged behaviour)
				int itemIndex = 0;
				int itemTotal = itemWriteMap.Count;

				foreach (var entry in itemWriteMap)
				{
					foreach (var tab in entry.Value)
					{
						_writer.WriteUserDefinedProperties(entry.Key, tab.Key, tab.Value);
					}

					itemIndex++;

					if (itemIndex % 5 == 0 || itemIndex == itemTotal)
					{
						ProgressPercent = 70 + (int)(30.0 * itemIndex / Math.Max(itemTotal, 1));
						ProgressText = $"Writing item {itemIndex} of {itemTotal}...";

						await System.Windows.Application.Current.Dispatcher.InvokeAsync(
							() => { },
							System.Windows.Threading.DispatcherPriority.Background);
					}
				}

				ProgressPercent = 100;
				ProgressText = $"Complete. Matched: {matched}, Unmatched: {unmatched}";
				Status = ProgressText;
			}
			catch (Exception ex)
			{
				Status = "Failed: " + ex.Message;
			}
			finally
			{
				IsBusy = false;
			}
		}
	}

}