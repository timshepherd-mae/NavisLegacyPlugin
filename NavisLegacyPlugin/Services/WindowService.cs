using System.Windows;

namespace NavisLegacyPlugin
{
	internal static class WindowService
	{
		public static void ShowModalOwned(Window window)
		{
			NavisworksWindowHelper.SetOwner(window);
			window.ShowDialog();
		}
	}
}
