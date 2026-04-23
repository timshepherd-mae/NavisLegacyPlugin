using Autodesk.Navisworks.Api.Plugins;

namespace NavisLegacyPlugin.UI.Ribbon
{
	[Plugin(
		"NavisLegacyPlugin_Launch",
		"MAE1",
		DisplayName = "Legacy Msg",
		ToolTip = "Show Legacy WPF Window")]
	[Autodesk.Navisworks.Api.Plugins.RibbonTab("TEST_TAB", DisplayName = "TEST Tools")]
	[Command(
		"LaunchLegacyWindow",
		DisplayName = "Legacy Msg",
		ToolTip = "Open the Legacy WPF window",
		Icon = "NavisLegacyPlugin.Resources.icon-abacus.png",
		LargeIcon = "NavisLegacyPlugin.Resources.icon-abacus.png")]
	public class LaunchLegacyWindowCommand : CommandHandlerPlugin
	{
		public override int ExecuteCommand(string name, params string[] parameters)
		{
			// Delegate to your existing entry logic
			return MainEntryPointLauncher.Run();
		}
	}
}
