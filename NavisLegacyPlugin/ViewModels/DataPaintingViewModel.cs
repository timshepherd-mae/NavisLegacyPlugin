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
using NavisLegacyPlugin.Services.Lookups;
using NavisLegacyPlugin.Services.DataSources;
using NavisLegacyPlugin.UI;

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
		public ICommand EditPropertyTabCommand { get; }
		public ICommand EditPropertyNameCommand { get; }
		public ICommand EditPropertyValueCommand { get; }
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

		public enum ModelDepthOption
		{
			All,
			Branch
		}

		private ModelDepthOption _modelDepth = ModelDepthOption.Branch;
		public ModelDepthOption ModelDepth
		{
			get => _modelDepth;
			set { _modelDepth = value; OnPropertyChanged(); }
		}

		private bool _overwrite = false;
		public bool Overwrite
		{
			get => _overwrite;
			set { _overwrite = value; OnPropertyChanged(); }
		}

		// --- Property Write Inputs ---
		private string _propertyTabName = "";
		public string PropertyTabName
		{
			get => _propertyTabName;
			set { _propertyTabName = value; OnPropertyChanged(); }
		}

		private string _propertyName = "";
		public string PropertyName
		{
			get => _propertyName;
			set { _propertyName = value; OnPropertyChanged(); }
		}

		private string _propertyValue = "";
		public string PropertyValue
		{
			get => _propertyValue;
			set { _propertyValue = value; OnPropertyChanged(); }
		}

		public DataPaintingViewModel(ComPropertyWriteService writer)
		{
			ModelDepth = ModelDepthOption.Branch;

			_writer = writer;
			_paintingService = new DataPaintingService(_modelLookupService, _writer);

			WriteTestCommand = new RelayCommand(WriteTest);

			EditPropertyTabCommand = new RelayCommand(() => EditField(nameof(PropertyTabName)));
			EditPropertyNameCommand = new RelayCommand(() => EditField(nameof(PropertyName)));
			EditPropertyValueCommand = new RelayCommand(() => EditField(nameof(PropertyValue)));

			GetSynchroDataCommand = new RelayCommand(GetSynchroData);

			GeometrySelectionService.SelectionChanged += OnSelectionChanged;
			UpdateTransferState();

			TransferRidCommand = new RelayCommand(TransferRid, () => CanTransferRid);
		}

		private void WriteTest()
		{
			// --- Basic validation ---
			if (string.IsNullOrWhiteSpace(PropertyTabName))
			{
				Status = "Please enter a Property Tab Name.";
				return;
			}

			if (string.IsNullOrWhiteSpace(PropertyName))
			{
				Status = "Please enter a Property Name.";
				return;
			}

			// Value can be empty, but not null
			var value = PropertyValue ?? string.Empty;

			// --- Build property dictionary ---
			var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
			{
				{ PropertyName.Trim(), value }
			};

			// --- Model Depth handling ---
			bool writeToLeafItems = (ModelDepth == ModelDepthOption.All);

			try
			{
				IsBusy = true;
				Status = "Writing property...";

				// --- Write using existing service ---
				_writer.WriteToCurrentSelection(
					PropertyTabName.Trim(),
					props,
					writeToLeafItems
				);

				Status = "Write complete.";
			}
			catch (Exception ex)
			{
				Status = $"Error: {ex.Message}";
				System.Diagnostics.Debug.WriteLine(ex);
			}
			finally
			{
				IsBusy = false;
			}
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

			for (int i = table.Rows.Count - 1; i >= 0; i--)
			{
				var rid = table.Rows[i]["MAE-4D.RID"]?.ToString();

				if (string.IsNullOrWhiteSpace(rid))
				{
					table.Rows.RemoveAt(i);
				}
			}

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
				WriteToLeafItems = string.Equals(WriteMode, "Leaf", StringComparison.OrdinalIgnoreCase),
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
			var groups = items
				.GroupBy(i => i.InstanceGuid.ToString("D"))
				.ToList();

			var duplicates = groups.Where(g => g.Count() > 1).ToList();

			return items
				.GroupBy(item => item.InstanceGuid.ToString("D"))
				.ToDictionary(
					g => g.Key,
					g => g.First(),
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
					Overwrite = Overwrite
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

		private void EditField(string fieldName)
		{
			string currentValue = "";

			switch (fieldName)
			{
				case nameof(PropertyTabName):
					currentValue = PropertyTabName;
					break;

				case nameof(PropertyName):
					currentValue = PropertyName;
					break;

				case nameof(PropertyValue):
					currentValue = PropertyValue;
					break;
			}

			string label = "";

			switch (fieldName)
			{
				case nameof(PropertyTabName):
					label = "Property Tab";
					break;

				case nameof(PropertyName):
					label = "Property Name";
					break;

				case nameof(PropertyValue):
					label = "Property Value";
					break;
			}

			var dialog = new InputDialog(currentValue, label);

			if (dialog.ShowDialog() == true)
			{
				switch (fieldName)
				{
					case nameof(PropertyTabName):
						PropertyTabName = dialog.Result;
						break;

					case nameof(PropertyName):
						PropertyName = dialog.Result;
						break;

					case nameof(PropertyValue):
						PropertyValue = dialog.Result;
						break;
				}
			}
		}


		public void CaptureSelectionA()
		{
			var selection = Application.ActiveDocument.CurrentSelection.SelectedItems;
			CollectionA = selection
				.Cast<ModelItem>()
				.SelectMany(item => ResolveByDepth(item))
				.Distinct()
				.ToList();
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
			CollectionB = selection
				.Cast<ModelItem>()
				.SelectMany(item => ResolveByDepth(item))
				.Distinct()
				.ToList();
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

		private IEnumerable<ModelItem> ResolveByDepth(ModelItem item)
		{
			var results = new List<ModelItem>();

			switch (ModelDepth)
			{
				case ModelDepthOption.All:
					CollectAllItems(item, results);
					break;

				case ModelDepthOption.Branch:
					CollectBranchItems(item, results);
					break;
			}

			return results;
		}

		private void CollectAllItems(ModelItem item, List<ModelItem> results)
		{
			if (item == null) return;

			if (!results.Contains(item))
				results.Add(item);

			if (item.Children != null)
			{
				foreach (var child in item.Children)
					CollectAllItems(child, results);
			}
		}

		private void CollectBranchItems(ModelItem item, List<ModelItem> results)
		{
			if (item == null) return;

			// include item if it has children (root/branch/sub-branch)
			if (item.Children != null && item.Children.Any())
			{
				if (!results.Contains(item))
					results.Add(item);

				foreach (var child in item.Children)
					CollectBranchItems(child, results);
			}
		}

	}
}
