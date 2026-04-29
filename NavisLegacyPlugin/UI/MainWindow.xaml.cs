using System;
using System.Windows;
using System.Windows.Controls;
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
