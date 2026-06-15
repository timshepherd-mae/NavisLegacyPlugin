
using System.Windows;
using System.Windows.Controls;
using Autodesk.Navisworks.Api.Plugins;

namespace NavisLegacyPlugin.UI
{
	public partial class GuidTestTab : UserControl
	{
		public GuidTestTab()
		{
			InitializeComponent();
		}

		private void GenerateMaeGuids_Click(object sender, RoutedEventArgs e)
		{
			var pluginRecord = Autodesk.Navisworks.Api.Application.Plugins
				.FindPlugin("GenerateMaeGuids.MAE");

			if (pluginRecord == null)
				return;

			var plugin = pluginRecord.LoadedPlugin as AddInPlugin;

			plugin?.Execute();
		}
	}
}
