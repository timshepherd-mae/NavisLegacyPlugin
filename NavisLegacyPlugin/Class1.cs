using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Plugins;
using NvApp = Autodesk.Navisworks.Api.Application;

namespace NavisLegacyPlugin
{
	[PluginAttribute("NavisLegacyPlugin", "TEST", DisplayName = "Legacy Msg", ToolTip = "4.8 Framework Plugin")]
	[AddInPluginAttribute(AddInLocation.AddIn)]

	public class MainEntryPoint : AddInPlugin
	{
		private static MainWindow _window;
		public override int Execute(params string[] parameters)
		{
			
			if (_window == null || !_window.IsLoaded)
			{
				_window = new MainWindow();

				NavisworksWindowHelper.SetOwner(_window);
				_window.Closed += (_, __) => _window = null;

				_window.Show();
				_window.Activate();
			}
			else
			{
				_window.Activate();
			}

			return 0;

		}

	}

}