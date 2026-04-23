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
		public override int Execute(params string[] parameters)

		{
			var wnd = new MainWindow();
			NavisworksWindowHelper.SetOwner(wnd);
			wnd.Show();
			wnd.Activate();
			return 0;
		}
	}

}