using Autodesk.Navisworks.Api;

namespace NavisLegacyPlugin
{
	public class NavisworksContextService
	{
		public bool HasActiveDocument =>
			Application.ActiveDocument != null;

		public SelectionInfo GetSelectionInfo()
		{
			var doc = Application.ActiveDocument;

			if (doc == null)
				return SelectionInfo.Empty;

			return new SelectionInfo
			{
				SelectedItemCount =
					doc.CurrentSelection.SelectedItems.Count
			};
		}
	}
}
