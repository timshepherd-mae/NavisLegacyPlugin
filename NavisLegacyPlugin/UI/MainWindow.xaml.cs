using System.Windows;

namespace NavisLegacyPlugin
{
	public partial class MainWindow : Window
	{
		private readonly NavisworksContextService _contextService;

		public MainWindow(NavisworksContextService contextService)
		{
			InitializeComponent();
			_contextService = contextService;

			LoadContext();
		}

		private void LoadContext()
		{
			if (!_contextService.HasActiveDocument)
			{
				StatusText.Text = "No active Navisworks document.";
				return;
			}

			var selection = _contextService.GetSelectionInfo();

			StatusText.Text =
				$"Selected items: {selection.SelectedItemCount}";
		}

		private void Close_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}
	}
}
