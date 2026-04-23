using Autodesk.Navisworks.Api.Plugins;

namespace NavisLegacyPlugin
{
	[Plugin(
		"NavisLegacyPlugin",        // Internal name (must be unique)
		"MAE1",                     // Vendor ID (2–4 chars)
		DisplayName = "Legacy Msg",
		ToolTip = "WPF Navisworks Plugin")]
	[AddInPlugin(AddInLocation.AddIn)]
	public class MainEntryPoint : AddInPlugin
	{
		private static MainWindow _window;

		public override int Execute(params string[] parameters)
		{
			Logger.Info("Add-in executed");

			if (_window == null || !_window.IsLoaded)
			{
				var contextService = new NavisworksContextService();

				_window = new MainWindow(contextService);
				WindowService.ShowModalOwned(_window);

				_window.Closed += (_, __) => _window = null;
			}
			else
			{
				_window.Activate();
			}

			return 0;
		}
	}
}
