using System.Windows;
using NavisLegacyPlugin.ViewModels;

namespace NavisLegacyPlugin
{
	public partial class MainWindow : Window
	{
		public MainWindow(NavisworksContextService contextService)
		{
			InitializeComponent();

			DataContext = new MainWindowViewModel(contextService);
		}

	}

}
