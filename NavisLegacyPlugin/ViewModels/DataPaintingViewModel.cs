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
			private set
			{
				_status = value;
				OnPropertyChanged();
			}
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

				// ✅ Synchro export column -> Navis logical target
				var columnMap = new Dictionary<string, string>
				{
					{ "3DUF:Synchro_SynchroID", "Synchro.SynchroID" },
					{ "3DUF:RID", "Synchro.RID" }
				};

				var table = _csvService.ReadCsv(
					filePath, 
					startRow,
					"3DUF:RID"
				);

				System.Diagnostics.Debug.WriteLine($"Reading file: {filePath}");
				System.Diagnostics.Debug.WriteLine($"CSV rows loaded: {table.Rows.Count}");

				System.Diagnostics.Debug.WriteLine("CSV Columns:");
				foreach (DataColumn col in table.Columns)
				{
					System.Diagnostics.Debug.WriteLine($"'{col.ColumnName}'");
				}

				// ✅ Build lookup once for performance
				var synchroLookup = BuildSynchroLookup();

				int rowIndex = 0;
				int matchedCount = 0;
				int unmatchedCount = 0;

				foreach (DataRow row in table.Rows)
				{
					var mapped = PropertyMappingHelper.MapRow(row, columnMap);

					if (!mapped.ContainsKey("Synchro.SynchroID"))
					{
						System.Diagnostics.Debug.WriteLine($"Row {rowIndex}: missing Synchro.SynchroID");
						rowIndex++;
						continue;
					}

					string synchroIdValue = mapped["Synchro.SynchroID"];

					if (string.IsNullOrWhiteSpace(synchroIdValue))
					{
						System.Diagnostics.Debug.WriteLine($"Row {rowIndex}: blank Synchro.SynchroID");
						rowIndex++;
						continue;
					}

					// Remove match key from write set
					var writeProperties = new Dictionary<string, string>(mapped, StringComparer.OrdinalIgnoreCase);
					writeProperties.Remove("Synchro.SynchroID");

					if (writeProperties.Count == 0)
					{
						System.Diagnostics.Debug.WriteLine($"Row {rowIndex}: no writable properties");
						rowIndex++;
						continue;
					}

					if (!synchroLookup.ContainsKey(synchroIdValue))
					{
						System.Diagnostics.Debug.WriteLine($"❌ No Navis match for SynchroID = {synchroIdValue}");
						unmatchedCount++;
						rowIndex++;
						continue;
					}

					var targetItem = synchroLookup[synchroIdValue];

					// ✅ Split "Tab.Property" -> group by tab
					var groupedByTab = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

					foreach (var kvp in writeProperties)
					{
						string fullKey = kvp.Key;
						string value = kvp.Value;

						var parts = fullKey.Split('.');

						if (parts.Length != 2)
						{
							System.Diagnostics.Debug.WriteLine($"Invalid property format: {fullKey}");
							continue;
						}

						string tabName = parts[0];
						string propName = parts[1];

						if (!groupedByTab.ContainsKey(tabName))
						{
							groupedByTab[tabName] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
						}

						groupedByTab[tabName][propName] = value;
					}

					foreach (var tab in groupedByTab)
					{
						string tabName = tab.Key;
						var propsForTab = tab.Value;

						_writer.WriteUserDefinedProperties(
							targetItem,
							tabName,
							propsForTab);

						System.Diagnostics.Debug.WriteLine($"✅ Applied tab '{tabName}' to SynchroID = {synchroIdValue}");

						foreach (var kvp in propsForTab)
						{
							System.Diagnostics.Debug.WriteLine($"   {kvp.Key} = {kvp.Value}");
						}
					}

					matchedCount++;
					rowIndex++;
				}

				Status = $"Loaded {table.Rows.Count} rows. Matched: {matchedCount}, Unmatched: {unmatchedCount}";
			}
			catch (Exception ex)
			{
				Status = "Failed: " + ex.Message;
			}
		}

		private Dictionary<string, ModelItem> BuildSynchroLookup()
		{
			var lookup = new Dictionary<string, ModelItem>(StringComparer.OrdinalIgnoreCase);

			var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
			if (doc == null)
				return lookup;

			foreach (Model model in doc.Models)
			{
				foreach (ModelItem item in model.RootItem.DescendantsAndSelf)
				{
					foreach (PropertyCategory cat in item.PropertyCategories)
					{
						if (!string.Equals(cat.DisplayName, "Synchro", StringComparison.OrdinalIgnoreCase))
							continue;

						foreach (DataProperty prop in cat.Properties)
						{
							if (!string.Equals(prop.DisplayName, "SynchroID", StringComparison.OrdinalIgnoreCase))
								continue;

							var value = prop.Value != null ? prop.Value.ToDisplayString() : null;

							if (!string.IsNullOrWhiteSpace(value) && !lookup.ContainsKey(value))
							{
								lookup[value] = item;
							}
						}
					}
				}
			}

			System.Diagnostics.Debug.WriteLine($"Lookup built: {lookup.Count} Synchro items");
			return lookup;
		}
	}
}
