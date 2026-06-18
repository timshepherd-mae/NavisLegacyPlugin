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

		public List<ModelItem> CollectionA { get; private set; } = new List<ModelItem>();
		public List<ModelItem> CollectionB { get; private set; } = new List<ModelItem>();

		public int CollectionACount => CollectionA.Count;
		public int CollectionBCount => CollectionB.Count;

		public ICommand WriteTestCommand { get; }
		public ICommand GetSynchroDataCommand { get; }
		public ICommand TransferRidCommand { get; }

		public ICommand CaptureSelectionACommand => new RelayCommand(CaptureSelectionA);
		public ICommand ClearSelectionACommand => new RelayCommand(ClearSelectionA);
		public ICommand ShowSelectionACommand => new RelayCommand(ShowSelectionA);
		public ICommand CaptureSelectionBCommand => new RelayCommand(CaptureSelectionB);
		public ICommand ClearSelectionBCommand => new RelayCommand(ClearSelectionB);
		public ICommand ShowSelectionBCommand => new RelayCommand(ShowSelectionB);

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

		
		private async System.Threading.Tasks.Task<(int matched, int unmatched)> ExecuteSelectionTransferAsync()
		{
			var table = BuildSelectionDataTable(CollectionA);
			var lookup = BuildSelectionLookup(CollectionB);

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

		private Dictionary<string, ModelItem> BuildSelectionLookup(ModelItemCollection selection)
		{
			return selection
				.Cast<ModelItem>()
				.ToDictionary(
					item => item.InstanceGuid.ToString("D"),
					item => item,
					StringComparer.OrdinalIgnoreCase);
		}

		private Dictionary<string, ModelItem> BuildSelectionLookup(IEnumerable<ModelItem> items)
		{
			return items
				.ToDictionary(
					item => item.InstanceGuid.ToString("D"),
					item => item,
					StringComparer.OrdinalIgnoreCase);
		}


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

		public void CaptureSelectionA()
		{
			var selection = Application.ActiveDocument.CurrentSelection.SelectedItems;
			CollectionA = selection.Cast<ModelItem>().ToList();
			OnPropertyChanged(nameof(CollectionACount));
			UpdateTransferState();
			CommandManager.InvalidateRequerySuggested();
		}

		public void ClearSelectionA()
		{
			CollectionA.Clear();
			OnPropertyChanged(nameof(CollectionACount));
			UpdateTransferState();
			CommandManager.InvalidateRequerySuggested();
		}

		public void ShowSelectionA()
		{
			var doc = Application.ActiveDocument;
			if (doc == null) return;
			doc.CurrentSelection.Clear();
			foreach (var item in CollectionA)
			{
				doc.CurrentSelection.Add(item);
			}
		}

		public void CaptureSelectionB()
		{
			var selection = Application.ActiveDocument.CurrentSelection.SelectedItems;
			CollectionB = selection.Cast<ModelItem>().ToList();
			OnPropertyChanged(nameof(CollectionBCount));
			UpdateTransferState();
			CommandManager.InvalidateRequerySuggested();
		}

		public void ClearSelectionB()
		{
			CollectionB.Clear();
			OnPropertyChanged(nameof(CollectionBCount));
			UpdateTransferState();
			CommandManager.InvalidateRequerySuggested();
		}

		public void ShowSelectionB()
		{
			var doc = Application.ActiveDocument;
			if (doc == null) return;
			doc.CurrentSelection.Clear();
			foreach (var item in CollectionB)
			{
				doc.CurrentSelection.Add(item);
			}
		}


		private void UpdateTransferState()
		{
			var hasA = CollectionA != null && CollectionA.Count > 0;
			var hasB = CollectionB != null && CollectionB.Count > 0;

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

		private DataTable BuildSelectionDataTable(IEnumerable<ModelItem> items)
		{
			var table = new DataTable();

			table.Columns.Add("InstanceGuid", typeof(string));
			table.Columns.Add("MAE-4D.RID", typeof(string));

			foreach (var item in items)
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
