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
using NavisLegacyPlugin.Models.Execution;
using NavisLegacyPlugin.Services.Lookups;
using NavisLegacyPlugin.Services.DataSources;
using NavisLegacyPlugin.Services.Execution;
using NavisLegacyPlugin.UI;
using NavisLegacyPlugin.Services.SelectionSets;
using NavisLegacyPlugin.Services.Collections;
using NavisLegacyPlugin.Services.ExternalSources;
using NavisLegacyPlugin.Services.Matching;
using NavisLegacyPlugin.Commands;
using System.Windows;

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
                InvalidateExternalTransfer();
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
                InvalidateExternalTransfer();
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
        public ICommand ClearExternalTransferCommand { get; }
        public ICommand FindBlankRidCandidatesCommand { get; }
        public ICommand PurgeBlankRidCommand { get; }
        public ICommand PrepareClearRidCommand { get; }
        public ICommand ClearRidCommand { get; }


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

        private bool _isExternalTransferValidated;
        public bool IsExternalTransferValidated
        {
            get { return _isExternalTransferValidated; }
            private set { _isExternalTransferValidated = value; OnPropertyChanged(); }
        }

        private TransferOutcomeSet _lastOutcomeSet;
        public TransferOutcomeSet LastOutcomeSet { get { return _lastOutcomeSet; } private set { _lastOutcomeSet = value; OnPropertyChanged(); } }
        private int _lastMatched;
        public int LastMatched { get { return _lastMatched; } private set { _lastMatched = value; OnPropertyChanged(); } }
        private int _lastUnmatched;
        public int LastUnmatched { get { return _lastUnmatched; } private set { _lastUnmatched = value; OnPropertyChanged(); } }
        private int _lastWritten;
        public int LastWritten { get { return _lastWritten; } private set { _lastWritten = value; OnPropertyChanged(); } }
        private int _lastSkipped;
        public int LastSkipped { get { return _lastSkipped; } private set { _lastSkipped = value; OnPropertyChanged(); } }
        private int _lastFailed;
        public int LastFailed { get { return _lastFailed; } private set { _lastFailed = value; OnPropertyChanged(); } }
        private int _lastSourceItems;
        public int LastSourceItems { get { return _lastSourceItems; } private set { _lastSourceItems = value; OnPropertyChanged(); } }
        private int _lastTransferableRids;
        public int LastTransferableRids { get { return _lastTransferableRids; } private set { _lastTransferableRids = value; OnPropertyChanged(); } }
        private int _lastMatchedTransferableRids;
        public int LastMatchedTransferableRids { get { return _lastMatchedTransferableRids; } private set { _lastMatchedTransferableRids = value; OnPropertyChanged(); } }
        private int _lastUnresolvedTransferableRids;
        public int LastUnresolvedTransferableRids { get { return _lastUnresolvedTransferableRids; } private set { _lastUnresolvedTransferableRids = value; OnPropertyChanged(); } }
        private int _lastMatchedWithoutTransferableRid;
        public int LastMatchedWithoutTransferableRid { get { return _lastMatchedWithoutTransferableRid; } private set { _lastMatchedWithoutTransferableRid = value; OnPropertyChanged(); } }  

        
        private int _lastUnresolvedWithoutTransferableRid;
        public int LastUnresolvedWithoutTransferableRid { get { return _lastUnresolvedWithoutTransferableRid; } private set { _lastUnresolvedWithoutTransferableRid = value; OnPropertyChanged(); } }
private void InvalidateExternalTransfer()
        {
            IsExternalTransferValidated = false;
            Status = "Not validated";
        }

        private bool TryValidateExternalTransfer()
        {
            return !string.IsNullOrWhiteSpace(ExternalSourceFilePath)
                && File.Exists(ExternalSourceFilePath)
                && !string.IsNullOrWhiteSpace(ExternalExportSetName)
                && Autodesk.Navisworks.Api.Application.ActiveDocument != null;
        }

        private void ValidateExternalTransfer()
        {
            IsExternalTransferValidated = TryValidateExternalTransfer();
            Status = IsExternalTransferValidated
                ? "Ready to execute external-to-live transfer"
                : "Not validated";
            Debug.WriteLine("[PHASE65B3] Validation completed valid=" + IsExternalTransferValidated + ".");
        }

        private void ClearExternalTransfer()
        {
            _externalSourceFilePath = string.Empty;
            OnPropertyChanged(nameof(ExternalSourceFilePath));
            _externalExportSetName = string.Empty;
            OnPropertyChanged(nameof(ExternalExportSetName));
            SourceResolutionType = null;
            TargetResolutionType = null;
            ExportResolutionType = null;
            Overwrite = false;
            ExpectDuplicateGuids = false;
            ProgressPercent = 0;
            ProgressText = string.Empty;
            LastMatched = 0;
            LastUnmatched = 0;
            LastWritten = 0;
            LastSkipped = 0;
            LastFailed = 0;
            LastSourceItems = 0;
            LastTransferableRids = 0;
            LastMatchedTransferableRids = 0;
            LastUnresolvedTransferableRids = 0;
            LastMatchedWithoutTransferableRid = 0;
            LastUnresolvedWithoutTransferableRid = 0;  
            LastOutcomeSet = null;
            IsExternalTransferValidated = false;
            Status = "Not validated";
            Debug.WriteLine("[PHASE65B3] UI hard reset completed.");
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
            ClearExternalTransferCommand = new RelayCommand(ClearExternalTransfer);
            FindBlankRidCandidatesCommand = new RelayCommand(FindBlankRidCandidates);
            PurgeBlankRidCommand = new RelayCommand(PurgeBlankRid);
            PrepareClearRidCommand = new RelayCommand(PrepareClearRid);
            ClearRidCommand = new RelayCommand(ClearRid);
            Status = "Not validated";
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

            var document = Autodesk.Navisworks.Api.Application.ActiveDocument;

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
            if (!IsExternalTransferValidated || !TryValidateExternalTransfer())
            {
                IsExternalTransferValidated = false;
                Status = "Not validated";
                return;
            }

            try
            {
                IsBusy = true;
                ProgressPercent = 0;
                ProgressText = string.Empty;
                Status = "Executing external-to-live transfer";

                CollectionResolutionType resolution =
                    ExportResolutionType ?? CollectionResolutionType.All;

                var request = new ExternalExportPopulationRequest(
                    ExternalSourceFilePath,
                    ExternalExportSetName.Trim(),
                    resolution);

                var externalProgress = new Progress<ExternalSourceProgress>(
                    progress => ApplyExternalSourceProgress(progress));
                ExternalExportPopulationResponse response = await
                    System.Threading.Tasks.Task.Run(
                        () => _externalExportPopulationService.Resolve(
                            request,
                            externalProgress));
                Stopwatch phase72Timing = Stopwatch.StartNew();
                LogPhase72Timing("External response returned to host", phase72Timing);
                ProgressText = "Preparing TARGET Selection Set...";
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(
                    () => { },
                    System.Windows.Threading.DispatcherPriority.Background);
                Stopwatch stageTiming = Stopwatch.StartNew();
                var document = Autodesk.Navisworks.Api.Application.ActiveDocument;
                var targetRoots = _selectionSetResolver.ResolveRequired(
                    document,
                    DataTransferSelectionSetNames.TargetPath);
                LogPhase72Timing("TARGET Selection Set resolved", stageTiming);
                ProgressText = "Resolving TARGET population...";
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(
                    () => { },
                    System.Windows.Threading.DispatcherPriority.Background);
                stageTiming.Restart();
                var targetItems = ResolveTransferPopulation(
                    targetRoots,
                    TargetResolutionType);
                LogPhase72Timing("TARGET population resolved", stageTiming);
                var targetLookup = new Dictionary<string, ModelItem>(
                    StringComparer.OrdinalIgnoreCase);
                LogPhase72Timing("TARGET diagnostics skipped in normal transfer path", phase72Timing);

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

                ProgressText = "Preparing SOURCE transfer data...";
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(
                    () => { },
                    System.Windows.Threading.DispatcherPriority.Background);
                LogPhase72Timing("Calling external transfer service", phase72Timing);
                var result = await _paintingService.ExecuteExternalSourceAsync(
                    response,
                    mapping,
                    targetLookup,
                    targetItems,
                    writeConfig,
                    progressConfig,
                    selectionSets);

                LastOutcomeSet = result.Outcomes;

                LastMatched = result.Matched;
                LastUnmatched = result.Unmatched;
                LastWritten = result.Written;
                LastSkipped = result.Skipped;
                LastFailed = result.Failed;
                ApplyExternalRidSummary(result.Outcomes);  
                Status = "transfer complete";
                Debug.WriteLine("[PHASE65B3] Transfer complete. Matched=" + LastMatched
                    + ", Unmatched=" + LastUnmatched + ", Written=" + LastWritten
                    + ", Skipped=" + LastSkipped + ", Failed=" + LastFailed + ".");
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                LastFailed = LastFailed + 1;
                Status = "Transfer failed";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ApplyExternalRidSummary(TransferOutcomeSet outcomeSet)
        {
            var outcomes = outcomeSet == null
                ? new List<TransferOutcome>()
                : outcomeSet.Outcomes.ToList();

            LastSourceItems = outcomes.Count;
            LastTransferableRids = outcomes.Count(HasTransferableRid);
            LastMatchedTransferableRids = outcomes.Count(
                outcome => HasTransferableRid(outcome) &&
                    outcome.Match.Status == TransferMatchStatus.Matched);
            LastUnresolvedTransferableRids = outcomes.Count(
                outcome => HasTransferableRid(outcome) &&
                    outcome.Match.Status != TransferMatchStatus.Matched);
            LastMatchedWithoutTransferableRid = outcomes.Count(
                outcome => !HasTransferableRid(outcome) &&
                    outcome.Match.Status == TransferMatchStatus.Matched);
            LastUnresolvedWithoutTransferableRid = outcomes.Count(
                outcome => !HasTransferableRid(outcome) &&
                    outcome.Match.Status != TransferMatchStatus.Matched);
        }

        private static bool HasTransferableRid(TransferOutcome outcome)
        {
            if (outcome == null || outcome.Payload == null)
                return false;

            TransferPayloadValue rid = outcome.Payload.FirstOrDefault(
                value => value != null &&
                    string.Equals(
                        value.Name,
                        "MAE-4D.RID",
                        StringComparison.OrdinalIgnoreCase));

            if (rid == null || string.IsNullOrWhiteSpace(rid.Value))
                return false;

            return !string.Equals(
                rid.Value.Trim(),
                "\"\"",
                StringComparison.Ordinal);
        }

        private static void LogPhase72Timing(string stage, Stopwatch stopwatch)
        {
            long elapsed = stopwatch == null ? -1L : stopwatch.ElapsedMilliseconds;
            string message = "[PHASE72 TIMING] " + stage + " elapsedMs=" + elapsed + ".";
            Debug.WriteLine(message);
            Logger.Info(message);
        }
        private void ApplyExternalSourceProgress(ExternalSourceProgress progress)
        {
            if (progress == null) return;
            ProgressText = string.IsNullOrWhiteSpace(progress.Message)
                ? "Reading external SOURCE..."
                : progress.Message;
            int percent;
            switch (progress.Stage)
            {
                case ExternalSourceProgressStage.StartingExternalSource:
                    percent = 1;
                    break;
                case ExternalSourceProgressStage.OpeningExternalFile:
                    percent = 5;
                    break;
                case ExternalSourceProgressStage.LocatingExportSet:
                    percent = 10;
                    break;
                case ExternalSourceProgressStage.ResolvingSourcePopulation:
                    percent = 15;
                    break;
                case ExternalSourceProgressStage.ReadingSourceData:
                    percent = progress.Total > 0
                        ? 15 + Math.Min(20, progress.Current * 20 / progress.Total)
                        : 15;
                    break;
                case ExternalSourceProgressStage.VerifyingSource:
                    percent = 34;
                    break;
                case ExternalSourceProgressStage.ExternalSourceReady:
                    percent = 35;
                    break;
                default:
                    percent = ProgressPercent;
                    break;
            }
            if (percent > ProgressPercent) ProgressPercent = percent;
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


        private async void FindBlankRidCandidates() { await PrepareRidCandidatesAsync(RidRemovalMode.BlankOnly); }
        private async void PrepareClearRid() { await PrepareRidCandidatesAsync(RidRemovalMode.AnyRid); }
        private async System.Threading.Tasks.Task PrepareRidCandidatesAsync(RidRemovalMode mode)
        {
            try
            {
                IsBusy = true; ProgressPercent = 0; ProgressText = "Preparing RID population..."; Status = "Inspecting RID values...";
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Background);
                var result = PurgeRidWorkflow.PrepareCandidates(mode, new Progress<string>(x => ProgressText = x), new Progress<int>(x => ProgressPercent = x));
                ProgressPercent = 50;
                Status = mode == RidRemovalMode.BlankOnly ? "Ready to purge " + result.Eligible + " blank RID items." : "Ready to clear RID from " + result.Eligible + " items.";
            }
            catch (Exception ex) { Status = "RID preparation failed: " + ex.Message; }
            finally { IsBusy = false; }
        }
        private async void PurgeBlankRid() { await RemoveRidAsync(RidRemovalMode.BlankOnly); }
        private async void ClearRid() { await RemoveRidAsync(RidRemovalMode.AnyRid); }
        private async System.Threading.Tasks.Task RemoveRidAsync(RidRemovalMode mode)
        {
            string warning = mode == RidRemovalMode.BlankOnly
                ? "Remove the currently eligible blank MAE-4D / RID properties?"
                : "This will permanently remove MAE-4D / RID properties, including populated or valid RID values, from RID-CLEAR-CANDIDATES. Continue?";
            if (System.Windows.MessageBox.Show(warning, mode == RidRemovalMode.BlankOnly ? "Purge Blank RID" : "CLEAR RID", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            try
            {
                IsBusy = true; ProgressPercent = 50; Status = "Removing RID properties...";
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Background);
                var result = PurgeRidWorkflow.RemoveCandidates(mode, new Progress<string>(x => ProgressText = x), new Progress<int>(x => ProgressPercent = x));
                ProgressPercent = 100; ProgressText = "Complete. " + result.Changed + " items changed; " + result.Removed + " RID properties removed."; Status = ProgressText;
            }
            catch (Exception ex) { Status = "RID operation failed: " + ex.Message; }
            finally { IsBusy = false; }
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
			var selection = Autodesk.Navisworks.Api.Application.ActiveDocument.CurrentSelection.SelectedItems;
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
			var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
			if (doc == null) return;
			doc.CurrentSelection.Clear();
			foreach (var item in CollectionA)
			{
				doc.CurrentSelection.Add(item);
			}
		}

		public void CaptureSelectionB()
		{
			var selection = Autodesk.Navisworks.Api.Application.ActiveDocument.CurrentSelection.SelectedItems;
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
			var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
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


	}  
}
