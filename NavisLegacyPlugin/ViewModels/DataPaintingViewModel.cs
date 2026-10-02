using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Input;

using Microsoft.Win32;

using Autodesk.Navisworks.Api;

using NavisLegacyPlugin.Helpers;
using NavisLegacyPlugin.Services;
using NavisLegacyPlugin.Models;
using NavisLegacyPlugin.Models.Collections;
using NavisLegacyPlugin.Models.ExternalSources;
using NavisLegacyPlugin.Services.Lookups;
using NavisLegacyPlugin.Services.DataSources;
using NavisLegacyPlugin.Services.Execution;
using NavisLegacyPlugin.UI;
using NavisLegacyPlugin.Services.SelectionSets;
using NavisLegacyPlugin.Services.Collections;
using NavisLegacyPlugin.Services.ExternalSources;
using NavisLegacyPlugin.Services.Matching;

namespace NavisLegacyPlugin.ViewModels
{
	public class DataPaintingViewModel : ViewModelBase
	{
		private readonly ComPropertyWriteService _writer;
		private readonly CsvDataService _csvService = new CsvDataService();
		private readonly ModelLookupService _modelLookupService = new ModelLookupService();
		private readonly DataPaintingService _paintingService;
		private readonly SelectionSetPathResolver _selectionSetResolver = new SelectionSetPathResolver();
        private readonly IExternalExportPopulationService _externalExportPopulationService;


        private bool _expectDuplicateGuids;
        public bool ExpectDuplicateGuids
        {
            get { return _expectDuplicateGuids; }
            set
            {
                if (_expectDuplicateGuids == value) return;
                _expectDuplicateGuids = value;
                DuplicateGuidMatchOptions.ExpectDuplicateGuids = value;
                OnPropertyChanged();
            }
        }

        private string _externalSourceFilePath = string.Empty;
        public string ExternalSourceFilePath
        {
            get { return _externalSourceFilePath; }
            set
            {
                if (_externalSourceFilePath == value) return;
                _externalSourceFilePath = value;
                OnPropertyChanged();
            }
        }

        private string _externalExportSetName = string.Empty;
        public string ExternalExportSetName
        {
            get { return _externalExportSetName; }
            set
            {
                if (_externalExportSetName == value) return;
                _externalExportSetName = value;
                OnPropertyChanged();
            }
        }

        private string _synchroDataFilePath = string.Empty;
		public string SynchroDataFilePath
        {
            get => _synchroDataFilePath;
            set 
			{				
				if (_synchroDataFilePath == value)
					return;
				
				_synchroDataFilePath = value; 
				OnPropertyChanged(); }
        }

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
		public ICommand EditExternalExportSetNameCommand { get; }
		public ICommand BrowseSynchroDataFileCommand {  get; }
		public ICommand GetSynchroDataCommand { get; }
		public ICommand TransferRidCommand { get; }
        public ICommand BrowseExternalSourceFileCommand { get; }
        public ICommand TransferExternalSourceCommand { get; }
        public ICommand ValidateExternalTransferCommand { get; }


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

        private string _transferValidationMessage = "Not validated.";
        public string TransferValidationMessage
        {
            get { return _transferValidationMessage; }
            private set { _transferValidationMessage = value; OnPropertyChanged(); }
        }

        private bool _isTransferValid;
        public bool IsTransferValid
        {
            get { return _isTransferValid; }
            private set { _isTransferValid = value; OnPropertyChanged(); }
        }

        private int _lastMatched;
        public int LastMatched { get { return _lastMatched; } private set { _lastMatched = value; OnPropertyChanged(); } }
        private int _lastUnmatched;
        public int LastUnmatched { get { return _lastUnmatched; } private set { _lastUnmatched = value; OnPropertyChanged(); } }

        private void ValidateExternalTransfer()
        {
            string message;
            IsTransferValid = TryValidateExternalTransfer(out message);
            TransferValidationMessage = message;
            Status = message;
            Debug.WriteLine("[PHASE65B] Validation completed valid=" + IsTransferValid + ", message='" + message + "'.");
        }

        private bool TryValidateExternalTransfer(out string message)
        {
            if (string.IsNullOrWhiteSpace(ExternalSourceFilePath) || !File.Exists(ExternalSourceFilePath))
            {
                message = "Select a valid external NWD/NWF source file.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(ExternalExportSetName))
            {
                message = "Enter the SOURCE Selection Set name.";
                return false;
            }
            if (Autodesk.Navisworks.Api.Application.ActiveDocument == null)
            {
                message = "Open the live TARGET Navisworks document.";
                return false;
            }
            message = "Ready to execute external-to-live transfer.";
            return true;
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

		private CollectionResolutionType? _sourceResolutionType;
		public CollectionResolutionType? SourceResolutionType
		{
			get => _sourceResolutionType;
			set { _sourceResolutionType = value; OnPropertyChanged(); }
		}

		private CollectionResolutionType? _targetResolutionType;
		public CollectionResolutionType? TargetResolutionType
		{
			get => _targetResolutionType;
			set { _targetResolutionType = value; OnPropertyChanged(); }
		}

		private CollectionResolutionType? _exportResolutionType;
		public CollectionResolutionType? ExportResolutionType
		{
			get => _exportResolutionType;
			set { _exportResolutionType = value; OnPropertyChanged(); }
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
			var executor = new ExecuteSignatureExecutor(_writer);
            _paintingService = new DataPaintingService(_modelLookupService, executor);
            _externalExportPopulationService = new ExternalExportPopulationService();

			WriteTestCommand = new RelayCommand(WriteTest);

			EditPropertyTabCommand = new RelayCommand(() => EditField(nameof(PropertyTabName)));
			EditPropertyNameCommand = new RelayCommand(() => EditField(nameof(PropertyName)));
			EditPropertyValueCommand = new RelayCommand(() => EditField(nameof(PropertyValue)));
			EditExternalExportSetNameCommand = new RelayCommand(() => EditField(nameof(ExternalExportSetName)));

			BrowseSynchroDataFileCommand = new RelayCommand(BrowseSynchroDataFile);

			GetSynchroDataCommand = new RelayCommand(GetSynchroData);

			GeometrySelectionService.SelectionChanged += OnSelectionChanged;
			UpdateTransferState();

			TransferRidCommand = new RelayCommand(TransferRid, () => CanTransferRid);
            BrowseExternalSourceFileCommand = new RelayCommand(BrowseExternalSourceFile);
            TransferExternalSourceCommand = new RelayCommand(TransferExternalSource);
            ValidateExternalTransferCommand = new RelayCommand(ValidateExternalTransfer);
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
				Status = "Resolving SOURCE and TARGET Selection Sets...";

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

            var document = Application.ActiveDocument;

            var sourceRoots =
                _selectionSetResolver.ResolveRequired(
                    document,
                    DataTransferSelectionSetNames.SourcePath);

            var sourceItems = ResolveTransferPopulation(
                sourceRoots,
                SourceResolutionType);

            var table =
                BuildSelectionDataTable(sourceItems);

            for (int i = table.Rows.Count - 1; i >= 0; i--)
			{
				var rid = table.Rows[i]["MAE-4D.RID"]?.ToString();

				if (string.IsNullOrWhiteSpace(rid))
				{
					table.Rows.RemoveAt(i);
				}
			}

            var targetRoots =
                _selectionSetResolver.ResolveRequired(
                    document,
                    DataTransferSelectionSetNames.TargetPath);

            var targetItems = ResolveTransferPopulation(
                targetRoots,
                TargetResolutionType);

            var lookup =
                BuildSelectionLookup(targetItems);

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

            var selectionSets = new ExecutionSelectionSets(
                DataTransferSelectionSetNames.SourcePath,
                DataTransferSelectionSetNames.TargetPath,
                DataTransferSelectionSetNames.ExportPath,
                SourceResolutionType,
                TargetResolutionType,
                ExportResolutionType);

			return await _paintingService.ExecuteAsync(
				dataSource,
				mapping,
				lookup,
				writeConfig,
				progressConfig,
                selectionSets);
		}

        private static ModelItemCollection ResolveTransferPopulation(
            IEnumerable<ModelItem> rootItems,
            CollectionResolutionType? resolutionType)
        {
            var population = new ModelItemCollection();

            if (rootItems == null)
                return population;

            if (!resolutionType.HasValue)
            {
                population.AddRange(rootItems);
                return population;
            }

            IModelItemCollectionResolver resolver =
                new ModelItemCollectionResolver();

            CollectionResolutionResult result =
                resolver.Resolve(rootItems);

            population.AddRange(
                result.GetItems(resolutionType.Value));

            return population;
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

        private void BrowseExternalSourceFile()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select external Navisworks source",
                Filter = "Navisworks files (*.nwd;*.nwf)|*.nwd;*.nwf|All files (*.*)|*.*",
                CheckFileExists = true
            };

            if (dialog.ShowDialog() == true)
                ExternalSourceFilePath = dialog.FileName;
        }

        private async void TransferExternalSource()
        {
            string validationMessage;
            IsTransferValid = TryValidateExternalTransfer(out validationMessage);
            TransferValidationMessage = validationMessage;
            if (!IsTransferValid)
            {
                Status = validationMessage;
                Debug.WriteLine("[PHASE65B] Execution blocked: " + validationMessage);
                return;
            }
            Debug.WriteLine("[PHASE65B] Execution requested. ExpectDuplicateGuids=" + ExpectDuplicateGuids);

            try
            {
                IsBusy = true;
                ProgressPercent = 0;
                ProgressText = "Opening external source...";
                Status = "Extracting external EXPORT population...";

                CollectionResolutionType resolution =
                    ExportResolutionType ?? CollectionResolutionType.All;

                var request = new ExternalExportPopulationRequest(
                    ExternalSourceFilePath,
                    ExternalExportSetName.Trim(),
                    resolution);

                ExternalExportPopulationResponse response = await
                    System.Threading.Tasks.Task.Run(
                        () => _externalExportPopulationService.Resolve(request));

                var document = Autodesk.Navisworks.Api.Application.ActiveDocument;

                var targetRoots = _selectionSetResolver.ResolveRequired(
                    document,
                    DataTransferSelectionSetNames.TargetPath);

                var targetItems = ResolveTransferPopulation(
                    targetRoots,
                    TargetResolutionType);

                // Phase 6.5A2
                // Ordered matching uses targetItems.
                // ExecuteSignature still expects a non-null lookup.
                var targetLookup =
                    new Dictionary<string, ModelItem>(
                        StringComparer.OrdinalIgnoreCase);

                WriteTargetPopulationDiagnostics(
                    targetRoots,
                    targetItems,
                    targetLookup);

                var mapping = new MappingConfig
                {
                    ColumnMap = new Dictionary<string, string>
                    {
                        { "InstanceGuid", "InstanceGuid" },
                        { "MAE-4D.RID", "MAE-4D.RID" }
                    },
                    MatchColumn = "InstanceGuid"
                };

                var writeConfig = new WriteConfig
                {
                    WriteToLeafItems = string.Equals(
                        WriteMode,
                        "Leaf",
                        StringComparison.OrdinalIgnoreCase),
                    Overwrite = Overwrite
                };

                var progressConfig = new ProgressConfig
                {
                    ProgressText = new Progress<string>(t => ProgressText = t),
                    ProgressPercent = new Progress<int>(p => ProgressPercent = p)
                };

                var selectionSets = new ExecutionSelectionSets(
                    DataTransferSelectionSetNames.SourcePath,
                    DataTransferSelectionSetNames.TargetPath,
                    DataTransferSelectionSetNames.ExportPath,
                    resolution,
                    TargetResolutionType,
                    resolution);

                var result = await _paintingService.ExecuteExternalSourceAsync(
                    response,
                    mapping,
                    targetLookup,
					targetItems,
                    writeConfig,
                    progressConfig,
                    selectionSets);

                Status = string.Format(
                    "External transfer complete. Matched: {0}, Unmatched: {1}",
                    result.matched,
                    result.unmatched);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                Status = "External transfer failed: " + ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void BrowseSynchroDataFile()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select Synchro data file",
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                CheckFileExists = true,
                CheckPathExists = true,
                Multiselect = false
            };

            if (!string.IsNullOrWhiteSpace(SynchroDataFilePath))
            {
                string existingDirectory = Path.GetDirectoryName(SynchroDataFilePath);

                if (!string.IsNullOrWhiteSpace(existingDirectory) && Directory.Exists(existingDirectory))
                {
                    dialog.InitialDirectory = existingDirectory;
                }

                dialog.FileName = Path.GetFileName(SynchroDataFilePath);
            }

            bool? result = dialog.ShowDialog();

            if (result == true)
            {
                SynchroDataFilePath = dialog.FileName;
            }
        }



        private async void GetSynchroData()
		{
            if (string.IsNullOrWhiteSpace(SynchroDataFilePath))
            {
                Status = "Please select a Synchro data CSV file.";
                return;
            }

            if (!File.Exists(SynchroDataFilePath))
            {
                Status = $"Synchro data CSV file not found: {SynchroDataFilePath}";
                return;
            }


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
					SynchroDataFilePath,
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

				LastMatched = result.matched;
                LastUnmatched = result.unmatched;
                Status = $"Complete. Matched: {result.matched}, Unmatched: {result.unmatched}";
                Debug.WriteLine("[PHASE65B] Result received matched=" + LastMatched + ", unmatched=" + LastUnmatched + ".");
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

				case nameof(ExternalExportSetName):
					currentValue = ExternalExportSetName;
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

				case nameof(ExternalExportSetName):
					label = "SOURCE Selection Set";
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

					case nameof(ExternalExportSetName):
						ExternalExportSetName = dialog.Result;
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

			// CanTransferRid = hasA && hasB;
			CanTransferRid = true;
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


        private static void WriteTargetPopulationDiagnostics(
            IEnumerable<ModelItem> targetRoots,
            IEnumerable<ModelItem> targetItems,
            Dictionary<string, ModelItem> targetLookup)
        {
            string logPath = Path.Combine(
                Path.GetTempPath(),
                "NavisLegacy_Phase65A_target_"
                + Guid.NewGuid().ToString("N")
                + ".target65a.log");

            try
            {
                using (StreamWriter log = new StreamWriter(
                    logPath,
                    false,
                    new UTF8Encoding(true)))
                {
                    log.AutoFlush = true;
                    WriteTargetLog(log, "TARGET POPULATION DIAGNOSTICS STARTED");
                    LogTargetItems(log, "TARGET ROOT", targetRoots);
                    LogTargetItems(log, "TARGET RESOLVED", targetItems);

                    int lookupCount = targetLookup == null ? 0 : targetLookup.Count;
                    WriteTargetLog(log, "TARGET LOOKUP count=" + lookupCount + ".");

                    if (targetLookup != null)
                    {
                        int index = 0;
                        foreach (KeyValuePair<string, ModelItem> pair in targetLookup)
                        {
                            index++;
                            WriteTargetLog(
                                log,
                                "TARGET LOOKUP item " + index
                                + ": Key=" + FormatTargetLogValue(pair.Key)
                                + ", " + DescribeTargetItem(pair.Value) + ".");
                        }
                    }

                    WriteTargetLog(log, "TARGET POPULATION DIAGNOSTICS FINISHED");
                }

                Debug.WriteLine(
                    "[MATCH65A] TARGET diagnostics file="
                    + FormatTargetLogValue(logPath) + ".");
            }
            catch (Exception exception)
            {
                Debug.WriteLine(
                    "[MATCH65A] TARGET diagnostics failed for '"
                    + logPath + "': " + exception);
            }
        }

        private static void LogTargetItems(
            StreamWriter log,
            string stage,
            IEnumerable<ModelItem> items)
        {
            if (items == null)
            {
                WriteTargetLog(log, stage + " collection=<null>.");
                return;
            }

            List<ModelItem> materialized = items.ToList();
            WriteTargetLog(log, stage + " count=" + materialized.Count + ".");

            var duplicateGuidGroups = materialized
                .Where(x => x != null)
                .GroupBy(x => x.InstanceGuid)
                .Where(x => x.Count() > 1)
                .ToList();

            WriteTargetLog(
                log,
                stage + " duplicate InstanceGuid groups="
                + duplicateGuidGroups.Count + ".");

            int index = 0;
            foreach (ModelItem item in materialized)
            {
                index++;
                WriteTargetLog(
                    log,
                    stage + " item " + index + ": "
                    + DescribeTargetItem(item) + ".");
            }
        }

        private static string DescribeTargetItem(ModelItem item)
        {
            if (item == null)
                return "item=<null>";

            return "API.InstanceGuid=" + FormatTargetLogValue(item.InstanceGuid.ToString("D"))
                + ", Item.GUID=" + FormatTargetLogValue(ReadTargetProperty(item, "Item", "GUID"))
                + ", Item.SourceFile=" + FormatTargetLogValue(ReadTargetSourceFile(item))
                + ", RID=" + FormatTargetLogValue(ReadTargetProperty(item, "MAE-4D", "RID"))
                + ", DisplayName=" + FormatTargetLogValue(item.DisplayName);
        }

        private static string ReadTargetSourceFile(ModelItem item)
        {
            string value = ReadTargetProperty(item, "Item", "Source File Name");
            return string.IsNullOrWhiteSpace(value)
                ? ReadTargetProperty(item, "Item", "Source File")
                : value;
        }

        private static string ReadTargetProperty(
            ModelItem item,
            string categoryName,
            string propertyName)
        {
            if (item == null || item.PropertyCategories == null)
                return null;

            foreach (PropertyCategory category in item.PropertyCategories)
            {
                if (category == null
                    || category.Properties == null
                    || !string.Equals(
                        category.DisplayName,
                        categoryName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                foreach (DataProperty property in category.Properties)
                {
                    if (property == null
                        || !string.Equals(
                            property.DisplayName,
                            propertyName,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    try
                    {
                        if (property.Value != null && property.Value.IsDisplayString)
                            return property.Value.ToDisplayString();

                        return property.Value == null ? null : property.Value.ToString();
                    }
                    catch (Exception exception)
                    {
                        return "<unreadable value: " + exception.GetType().Name + ">";
                    }
                }
            }

            return null;
        }

        private static void WriteTargetLog(StreamWriter log, string message)
        {
            string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
                + " [MATCH65A] HOST " + message;
            Debug.WriteLine(line);
            log.WriteLine(line);
        }

        private static string FormatTargetLogValue(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "<null>"
                : "'" + value + "'";
        }

	}
}