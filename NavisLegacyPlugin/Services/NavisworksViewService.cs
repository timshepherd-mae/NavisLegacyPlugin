using Autodesk.Navisworks.Api;

namespace NavisLegacyPlugin.Services
{
	public class NavisworksViewService
	{
		public void SelectAndZoomTo(ModelItem item)
		{
			if (item == null)
				return;

			var doc = Application.ActiveDocument;
			if (doc == null)
				return;

			doc.CurrentSelection.Clear();
			doc.CurrentSelection.Add(item);

			// This exists in all Navisworks versions
			doc.ActiveView.RequestFocus();
		}
	}
}
