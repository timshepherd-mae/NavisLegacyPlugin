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
		private readonly DataPaintingService _paintingService;

		public ICommand WriteTestCommand { get; }
		public ICommand GetDataCommand { get; }

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
			set
			{
				_writeMode = value;
				OnPropertyChanged();
			}
		}

		public DataPaintingViewModel(ComPropertyWriteService writer)
		{
			_writer = writer;

			_paintingService = new DataPaintingService(
				_modelLookupService,
				_writer);

			WriteTestCommand = new RelayCommand(WriteTest);
			GetDataCommand = new RelayCommand(GetData);
		}


		private void WriteTest()
		{
			var props = new Dictionary<string, string>
		{
			{ "RID", DateTime.Now.ToString("HHmmss") }
		};

			_writer.WriteToCurrentSelection("Synchro", props, false);
		}

		private void CollectLeafItems(ModelItem item, List<ModelItem> results)
		{
			if (item == null) return;

			if (item.Children == null || !item.Children.Any())
			{
				if (!results.Contains(item))
					results.Add(item);
				return;
			}

			foreach (ModelItem child in item.Children)
			{
				CollectLeafItems(child, results);
			}
		}

		private async void GetData()
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
				var csvProgress = new Progress<int>(rows =>
				{
					ProgressText = $"Reading CSV... {rows}";
				});

				var table = _csvService.ReadCsvWithProgress(
					@"C:\Users\tshepherd\OneDrive - Murphy\_dev\Synchro\ResourceTransfer\StFergus Unit Test\Tranfer Process Test\data.csv",
					2,
					"3DUF:RID",
					csvProgress);

				ProgressPercent = 15;


				ProgressPercent = 35;

				await System.Windows.Application.Current.Dispatcher.InvokeAsync(
					() => { },
					System.Windows.Threading.DispatcherPriority.Background);


				var result = await _paintingService.ExecuteAsync(
					table,
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
	}

}