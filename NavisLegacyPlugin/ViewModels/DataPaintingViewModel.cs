using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows.Input;
using NavisLegacyPlugin.Helpers;
using NavisLegacyPlugin.Services;

namespace NavisLegacyPlugin.ViewModels
{
	public class DataPaintingViewModel : ViewModelBase
	{
		private readonly ComPropertyWriteService _writer;
		private readonly CsvDataService _csvService = new CsvDataService();

		public ICommand WriteTestCommand { get; }
		public ICommand GetDataCommand { get; }

		private string _status = "Ready.";
		public string Status
		{
			get { return _status; }
			private set { _status = value; OnPropertyChanged(); }
		}

		// HARD-CODED TEST VALUES (replace these)
		private const string TestGuidString = "68bdb303-1e93-582a-b49e-938165be3a61";
		private const string TestTabName = "MAE";
		private const string TestPropName = "PaintTest";
		private const string TestPropValue = "Hello from Data Painting";

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


				var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
				{
					{ "RID", DateTime.Now.ToString("HHmmss") },
					{ "RID_2", "SECOND_" + DateTime.Now.ToString("HHmmss") },
					{ "RID_3", "THIRD_" + DateTime.Now.ToString("HHmmss") }	
				};

				_writer.WriteToCurrentSelection("Synchro", props);

				Status = "Write complete.";
			}
			catch (Exception ex)
			{
				Status = "Failed: " + ex.Message;
			}
		}

		private void GetData()
		{
			try
			{
				Status = "Reading CSV...";

				// ✅ HARD-CODED TEST SETTINGS
				string filePath = @"C:\Users\tshepherd\OneDrive - Murphy\_dev\Synchro\ResourceTransfer\StFergus Unit Test\Tranfer Process Test\datapaint_import_01.csv";
				int startRow = 2;

				var columnMap = new Dictionary<string, string>
		{
			{ "RID", "RID" },
			{ "TaskName", "TaskName" }
		};

				var table = _csvService.ReadCsv(filePath, startRow);

				int rowIndex = 0;

				foreach (DataRow row in table.Rows)
				{
					var mapped = PropertyMappingHelper.MapRow(row, columnMap);

					// ✅ DEBUG OUTPUT
					System.Diagnostics.Debug.WriteLine($"Row {rowIndex}");

					foreach (var kvp in mapped)
					{
						System.Diagnostics.Debug.WriteLine($"  {kvp.Key} = {kvp.Value}");
					}

					rowIndex++;
				}

				Status = $"Loaded {table.Rows.Count} rows.";
			}
			catch (Exception ex)
			{
				Status = "Failed: " + ex.Message;
			}
		}
	}
}