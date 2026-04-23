using System;
using System.Windows;
using System.Windows.Interop;
using Autodesk.Navisworks.Api;

namespace NavisLegacyPlugin
{
	internal static class NavisworksWindowHelper
	{
		public static void SetOwner(Window window)
		{
			IntPtr navisHandle =
				Autodesk.Navisworks.Api.Application.Gui.MainWindow.Handle;

			new WindowInteropHelper(window)
			{
				Owner = navisHandle
			};
		}
	}
}