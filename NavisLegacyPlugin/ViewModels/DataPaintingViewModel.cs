using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Input;
using Autodesk.Navisworks.Api;
using NavisLegacyPlugin.Helpers;
using NavisLegacyPlugin.Services;
using NavisLegacyPlugin.Models;
using System.Data;

namespace NavisLegacyPlugin.ViewModels
{
	public class DataPaintingViewModel : ViewModelBase
	{
		private readonly ComPropertyWriteService _writer;
		private readonly CsvDataService _csvService = new CsvDataService();
		private readonly ModelLookupService _modelLookupService = new ModelLookupService();
		private readonly DataPaintingService _paintingService;

		private bool _canTransferRid;
		public bool CanTransferRid
		{
			get => _canTransferRid;
			private set
			{
				_canTransferRid = value;
				OnPropertyChanged(nameof(CanTransferRid));
			}
		}

		public ICommand WriteTestCommand { get; }
		public ICommand GetSynchroDataCommand { get; }
		public ICommand TransferRidCommand { get; }

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

		private string _writeMode = "Branch";
		public string WriteMode
		{
			get => _writeMode;
			set { _writeMode = value; OnPropertyChanged(); }
		}

		public DataPaintingViewModel(ComPropertyWriteService writer)
		{
			_writer = writer;
			_paintingService = new DataPaintingService(_modelLookupService, _writer);

			WriteTestCommand = new RelayCommand(WriteTest);
			GetSynchroDataCommand = new RelayCommand(GetSynchroData);

			GeometrySelectionService.SelectionChanged += OnSelectionChanged;
			UpdateTransferState();

			TransferRidCommand = new RelayCommand(TransferRid, () => CanTransferRid);
		}

		private void WriteTest()
		{
			var props = new Dictionary<string, string>
			{
				{ "RID", DateTime.Now.ToString("HHmmss") }
			};

			_writer.WriteToCurrentSelection("Synchro", props, false);
		}

		private async void TransferRid()
		{
			try
			{
				IsBusy = true;
				Status = "Executing RID transfer...";

				var result = await ExecuteSelectionTransferAsync();

				Status = $"Complete. Matched: {result.matched}, Unmatched: {result.unmatched}";
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex);
				Status = $"Error: {ex.Message}";
			}
			finally
			{
				IsBusy = false;
			}
		}

		// ✅ Extracted, explicit selection pipeline
		private async System.Threading.Tasks.Task<(int matched, int unmatched)> ExecuteSelectionTransferAsync()
		{
			var table = BuildSelectionADataTable();
			var lookup = BuildSelectionLookup(GeometrySelectionService.SelectionB);

			var dataSource = new InMemoryDataSource(table);

			var mapping = new MappingConfig
			{
				ColumnMap = new Dictionary<string, string>
				{
					{ "InstanceGuid", "InstanceGuid" },   // ✅ REQUIRED
                    { "MAE-4D.RID", "MAE-4D.RID" }
				},
				MatchColumn = "InstanceGuid"
			};

			var writeConfig = new WriteConfig
			{
				WriteToLeafItems = string.Equals(WriteMode, "Leaf", StringComparison.OrdinalIgnoreCase)
			};

			var progressConfig = new ProgressConfig
			{
				ProgressText = new Progress<string>(t => ProgressText = t),
				ProgressPercent = new Progress<int>(p => ProgressPercent = p)
			};

			return await _paintingService.ExecuteAsync(
				dataSource,
				mapping,
				lookup,
				writeConfig,
				progressConfig);
		}

		// ✅ Consolidated GUID lookup helper
		private Dictionary<string, ModelItem> BuildSelectionLookup(ModelItemCollection selection)
		{
			return selection
				.Cast<ModelItem>()
				.ToDictionary(
					item => item.InstanceGuid.ToString("D"),
					item => item,
					StringComparer.OrdinalIgnoreCase);
		}

		// ✅ Existing CSV / Synchro path — unchanged
		private async void GetSynchroData()
		{
			try
			{
				IsBusy = true;
				ProgressText = "Reading CSV...";

				await System.Windows.Application.Current.Dispatcher.InvokeAsync(
					() => { },
					System.Windows.Threading.DispatcherPriority.Background);

				var mapping = new MappingConfig
				{
					ColumnMap = new Dictionary<string, string>
					{
						{ "3DUF:Synchro_SynchroID", "Synchro.SynchroID" },
						{ "3DUF:RID", "MAE-4D.RID" }
					},
					MatchColumn = "Synchro.SynchroID"
				};

				var lookupConfig = new LookupConfig
				{
					LookupTab = "Synchro",
					LookupProperty = "SynchroID"
				};

				var writeConfig = new WriteConfig
				{
					WriteToLeafItems = string.Equals(WriteMode, "Leaf", StringComparison.OrdinalIgnoreCase)
				};

				var progressConfig = new ProgressConfig
				{
					ProgressText = new Progress<string>(t => ProgressText = t),
					ProgressPercent = new Progress<int>(p => ProgressPercent = p)
				};

				var dataSource = new CsvDataSource(
					_csvService,
					@"C:\Users\tshepherd\OneDrive - Murphy\_dev\Synchro\ResourceTransfer\StFergus Unit Test\Tranfer Process Test\data.csv",
					2,
					"3DUF:RID"
				);

				ProgressPercent = 35;

				await System.Windows.Application.Current.Dispatcher.InvokeAsync(
					() => { },
					System.Windows.Threading.DispatcherPriority.Background);


				var result = await _paintingService.ExecuteAsync(
					dataSource,
					mapping,
					lookupConfig,
					writeConfig,
					progressConfig
				);

				Status = $"Complete. Matched: {result.matched}, Unmatched: {result.unmatched}";
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


		private void OnSelectionChanged()
		{
			UpdateTransferState();
		}

		private void UpdateTransferState()
		{
			var hasA = GeometrySelectionService.SelectionA != null
					   && GeometrySelectionService.SelectionA.Count > 0;

			var hasB = GeometrySelectionService.SelectionB != null
					   && GeometrySelectionService.SelectionB.Count > 0;

			CanTransferRid = hasA && hasB;
		}

		private DataTable BuildSelectionADataTable()
		{
			var table = new DataTable();

			table.Columns.Add("InstanceGuid", typeof(string));
			table.Columns.Add("MAE-4D.RID", typeof(string));

			var selectionA = GeometrySelectionService.SelectionA;

			foreach (ModelItem item in selectionA)
			{
				var row = table.NewRow();

				row["InstanceGuid"] = item.InstanceGuid.ToString("D");

				var prop = item.PropertyCategories
					.FindCategoryByDisplayName("MAE-4D")?
					.Properties
					.FindPropertyByDisplayName("RID");

				row["MAE-4D.RID"] = prop?.Value?.ToDisplayString() ?? "";

				table.Rows.Add(row);
			}

			return table;
		}

	}
}
