using System.Windows;
using System.Windows.Input;
using NavisLegacyPlugin.Services;
using NavisLegacyPlugin;

namespace NavisLegacyPlugin.ViewModels
{
	public class MainWindowViewModel : ViewModelBase
	{
		private readonly NavisworksContextService _contextService;

		private string _statusText;
		public string StatusText
		{
			get => _statusText;
			private set
			{
				_statusText = value;
				OnPropertyChanged();
			}
		}

		public string DebugMarker => "MainWindowViewModel is the DataContext";

		public ICommand ActionOneCommand { get; }
		public ICommand ActionTwoCommand { get; }
		public GetGeometryPositionsViewModel GeometryPositions { get; }

		public MainWindowViewModel(NavisworksContextService contextService)
		{
			_contextService = contextService;
			LoadContext();

			ActionOneCommand = new RelayCommand(OnActionOne);
			ActionTwoCommand = new RelayCommand(OnActionTwo);
			GeometryPositions = new GetGeometryPositionsViewModel(new GeometryPositionService());
		}

		private void LoadContext()
		{
			if (!_contextService.HasActiveDocument)
			{
				StatusText = "No active Navisworks document.";
				return;
			}

			var selection = _contextService.GetSelectionInfo();
			StatusText = $"Selected items: {selection.SelectedItemCount}";
		}

		private void OnActionOne()
		{
			MessageBox.Show(
				"Action One executed.",
				"Action One",
				MessageBoxButton.OK,
				MessageBoxImage.Information);

			System.Diagnostics.Debug.WriteLine("btn 1");
		}

		private void OnActionTwo()
		{
			MessageBox.Show(
				"Action Two executed.",
				"Action Two",
				MessageBoxButton.OK,
				MessageBoxImage.Information);
		}

	}
}
