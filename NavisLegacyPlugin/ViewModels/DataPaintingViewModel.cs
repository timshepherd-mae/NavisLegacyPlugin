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
			get { return _status; }
			private set
			{
				_status = value;
				OnPropertyChanged();
			}
		}

		private int _progressPercent;
		public int ProgressPercent
		{
			get { return _progressPercent; }
			set { _progressPercent = value; OnPropertyChanged(); }
		}

		private string _progressText = "";
		public string ProgressText
		{
			get { return _progressText; }
			set { _progressText = value; OnPropertyChanged(); }
		}

		private bool _isBusy;
		public bool IsBusy
		{
			get { return _isBusy; }
			set { _isBusy = value; OnPropertyChanged(); }
		}

		private string _writeMode = "Branch";
		public string WriteMode
		{
			get { return _writeMode; }
			set
			{
				if (_writeMode != value)
				{
					_writeMode = value;
					OnPropertyChanged(nameof(WriteMode));
				}
			}
		}

		public DataPaintingViewModel(ComPropertyWriteService writer)
		{
			_writer = writer;

			WriteTestCommand = new RelayCommand(WriteTest);
			GetDataCommand = new RelayCommand(GetData);
		}

		private void WriteTest()
		{
			try
			{
				Status = "Writing...";

				System.Diagnostics.Debug.WriteLine($"WriteMode = {WriteMode}");

				var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
				{
					{ "RID", DateTime.Now.ToString("HHmmss") },
					{ "RID_2", "SECOND_" + DateTime.Now.ToString("HHmmss") },
					{ "RID_3", "THIRD_" + DateTime.Now.ToString("HHmmss") }
				};

				bool writeToLeafItems =
					string.Equals(WriteMode, "Leaf", StringComparison.OrdinalIgnoreCase);

				_writer.WriteToCurrentSelection("MAE4D", props, writeToLeafItems);

				Status = "Write complete.";
			}
			catch (Exception ex)
			{
				Status = "Failed: " + ex.Message;
			}
		}

		private async void GetData()
		{
			try
			{
				IsBusy = true;
				ProgressPercent = 0;
				ProgressText = "Reading CSV...";
				Status = "Reading CSV...";

				await System.Windows.Application.Current.Dispatcher.InvokeAsync(
					() => { },
					System.Windows.Threading.DispatcherPriority.Background);

				string filePath = @"C:\Users\tshepherd\OneDrive - Murphy\_dev\Synchro\ResourceTransfer\StFergus Unit Test\Tranfer Process Test\data.csv";
				int startRow = 2;

				var columnMap = new Dictionary<string, string>
				{
					{ "3DUF:Synchro_SynchroID", "Synchro.SynchroID" },
					{ "3DUF:RID", "MAE-4D.RID" }
				};


				ProgressText = "Reading CSV...";

				var progress = new Progress<int>(rowsRead =>
				{
					ProgressText = $"Reading CSV... {rowsRead} rows";
				});

				var table = _csvService.ReadCsvWithProgress(
					filePath,
					startRow,
					"3DUF:RID",
					progress);


				ProgressPercent = 15;
				ProgressText = $"CSV loaded: {table.Rows.Count} rows";
				await System.Windows.Application.Current.Dispatcher.InvokeAsync(
					() => { },
					System.Windows.Threading.DispatcherPriority.Background);

				ProgressText = "Building Synchro lookup...";


				var lookupProgress = new Progress<int>(count =>
				{
					ProgressText = $"Building lookup... scanned {count} items";
				});

				var synchroLookup = await _modelLookupService.BuildLookupWithProgressAsync(
					"Synchro",
					"SynchroID",
					lookupProgress);

				ProgressPercent = 35;
				await System.Windows.Application.Current.Dispatcher.InvokeAsync(
					() => { },
					System.Windows.Threading.DispatcherPriority.Background);

				int rowIndex = 0;
				int matchedCount = 0;
				int unmatchedCount = 0;
				int total = table.Rows.Count;

				foreach (DataRow row in table.Rows)
				{
					rowIndex++;

					var mapped = PropertyMappingHelper.MapRow(row, columnMap);
					var instruction = PaintInstructionBuilder.Build(mapped, "Synchro.SynchroID");

					if (instruction == null || string.IsNullOrWhiteSpace(instruction.MatchValue))
						continue;

					if (!synchroLookup.ContainsKey(instruction.MatchValue))
					{
						unmatchedCount++;
					}
					else
					{
						var targetItem = synchroLookup[instruction.MatchValue];

						foreach (var tab in instruction.PropertiesByTab)
						{
							_writer.WriteUserDefinedProperties(
								targetItem,
								tab.Key,
								tab.Value);
						}

						matchedCount++;
					}

					if (rowIndex % 25 == 0)
					{
						ProgressPercent = 35 + (int)(65.0 * rowIndex / total);
						ProgressText = $"Processing row {rowIndex} of {total}...";
						await System.Windows.Application.Current.Dispatcher.InvokeAsync(
							() => { },
							System.Windows.Threading.DispatcherPriority.Background);
					}
				}

				ProgressPercent = 100;
				ProgressText = $"Complete. Matched: {matchedCount}, Unmatched: {unmatchedCount}";
				Status = ProgressText;
			}
			catch (Exception ex)
			{
				Status = "Failed: " + ex.Message;
				ProgressText = Status;
			}
			finally
			{
				IsBusy = false;
			}
		}

	}
}
