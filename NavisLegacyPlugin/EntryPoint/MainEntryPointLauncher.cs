namespace NavisLegacyPlugin
{
	internal static class MainEntryPointLauncher
	{
		private static MainWindow _window;

		public static int Run()
		{
			if (_window == null || !_window.IsLoaded)
			{
				var contextService = new NavisworksContextService();

				_window = new MainWindow(contextService);
				NavisworksWindowHelper.SetOwner(_window);

				_window.Closed += (_, __) => _window = null;
				_window.ShowDialog();
			}
			else
			{
				_window.Activate();
			}

			return 0;
		}
	}
}
