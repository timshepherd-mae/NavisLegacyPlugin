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

		private void GeometryGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (GeometryGrid.SelectedItem != null)
			{
				GeometryGrid.ScrollIntoView(GeometryGrid.SelectedItem);
			}
		}

		protected override void OnClosed(EventArgs e)
		{
			if (DataContext is GetGeometryPositionsViewModel vm)
			{
				vm.Dispose();
			}
			base.OnClosed(e);
		}

	}

}
