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

					// ✅ STEP 1 — Extract match key
					if (!mapped.ContainsKey("Synchro.SynchroID"))
						continue;

					string synchroIdValue = mapped["Synchro.SynchroID"];

					if (string.IsNullOrWhiteSpace(synchroIdValue))
						continue;

					System.Diagnostics.Debug.WriteLine($"Matching SynchroID = {synchroIdValue}");

					// ✅ STEP 2 — Prepare properties to WRITE
					var writeProperties = new Dictionary<string, string>(mapped);

					// ❗ Remove match key so we don't overwrite it
					writeProperties.Remove("Synchro.SynchroID");

					if (writeProperties.Count == 0)
						continue;

					// ✅ STEP 3 — Find matching Navis item
					var targetItem = FindItemByProperty(
						"Synchro",
						"SynchroID",
						synchroIdValue);

					if (targetItem == null)
					{
						System.Diagnostics.Debug.WriteLine($"❌ No match found for SynchroID = {synchroIdValue}");
						continue;
					}

					// ✅ STEP 4 — APPLY properties
					_writer.WriteUserDefinedProperties(targetItem, "Synchro", writeProperties);

					System.Diagnostics.Debug.WriteLine($"✅ Applied RID to item with SynchroID = {synchroIdValue}");
				}
				Status = $"Loaded {table.Rows.Count} rows.";
			}
			catch (Exception ex)
			{
				Status = "Failed: " + ex.Message;
			}
		}


		private ModelItem FindItemByProperty(string tabName, string propertyName, string value)
		{
			var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;

			if (doc == null)
				return null;

			foreach (Model model in doc.Models)
			{
				foreach (ModelItem item in model.RootItem.DescendantsAndSelf)
				{
					foreach (PropertyCategory cat in item.PropertyCategories)
					{
						if (!string.Equals(cat.DisplayName, tabName, StringComparison.OrdinalIgnoreCase))
							continue;

						foreach (DataProperty prop in cat.Properties)
						{
							if (!string.Equals(prop.DisplayName, propertyName, StringComparison.OrdinalIgnoreCase))
								continue;

							if (prop.Value != null &&
								string.Equals(prop.Value.ToDisplayString(), value, StringComparison.OrdinalIgnoreCase))
							{
								return item;
							}
						}
					}
				}
			}

			return null;
		}


	}
}