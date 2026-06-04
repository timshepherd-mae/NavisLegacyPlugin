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
				System.Diagnostics.Debug.WriteLine($"WriteMode = {WriteMode}");

				var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
				{
					{ "RID", DateTime.Now.ToString("HHmmss") },
					{ "RID_2", "SECOND_" + DateTime.Now.ToString("HHmmss") },
					{ "RID_3", "THIRD_" + DateTime.Now.ToString("HHmmss") }	
				};

				bool writeToLeafItems = string.Equals(WriteMode, "Leaf", StringComparison.OrdinalIgnoreCase);

				_writer.WriteToCurrentSelection("Synchro", props, writeToLeafItems);

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
				string filePath = @"C:\Users\tshepherd\OneDrive - Murphy\_dev\Synchro\ResourceTransfer\StFergus Unit Test\Tranfer Process Test\data.csv";
				int startRow = 2;


				var columnMap = new Dictionary<string, string>
				{
					{ "3DUF:Synchro_SynchroID", "Synchro.SynchroID" },
					{ "3DUF:RID", "Synchro.RID" }
				};

				var table = _csvService.ReadCsv(filePath, startRow);



				foreach (DataRow row in table.Rows)
				{
					var mapped = PropertyMappingHelper.MapRow(row, columnMap);

					// ✅ MATCH VALUE
					if (!mapped.ContainsKey("Synchro.SynchroID"))
						continue;

					string matchValue = mapped["Synchro.SynchroID"];

					// ✅ DEBUG
					System.Diagnostics.Debug.WriteLine($"Matching SynchroID = {matchValue}");

					// ✅ REMOVE MATCH KEY FROM WRITE SET
					// (we don’t want to overwrite it unintentionally)
					var writeProperties = new Dictionary<string, string>(mapped);
					writeProperties.Remove("Synchro.SynchroID");

					// ✅ TEMP: PRINT ONLY (SAFE STAGE)
					foreach (var kvp in writeProperties)
					{
						System.Diagnostics.Debug.WriteLine($"  WRITE {kvp.Key} = {kvp.Value}");
					}
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