using System.Collections.Generic;
using Autodesk.Navisworks.Api;
using NavisLegacyPlugin.Models;

namespace NavisLegacyPlugin.Services
{
	public class GeometryPositionService
	{
		public IList<GeometryPositionRow> GetGeometryPositions(bool includeSubObjects)
		{
			var results = new List<GeometryPositionRow>();

			var doc = Application.ActiveDocument;
			if (doc == null)
				return results;

			var selection = doc.CurrentSelection.SelectedItems;

			foreach (var item in selection)
			{
				CollectItem(item, includeSubObjects, results);
			}

			return results;
		}

		private void CollectItem(
			ModelItem item,
			bool includeSubObjects,
			IList<GeometryPositionRow> results)
		{
			AddItemIfGeometry(item, results);

			if (!includeSubObjects)
				return;

			foreach (var descendant in item.Descendants)
			{
				AddItemIfGeometry(descendant, results);
			}
		}

		private void AddItemIfGeometry(
			ModelItem item,
			IList<GeometryPositionRow> results)
		{
			if (item.Geometry == null)
				return;

			var bbox = item.BoundingBox();

			results.Add(new GeometryPositionRow
			{
				ModelItem = item,
				ItemGuid = item.InstanceGuid.ToString(),
				BoundingBoxMin = bbox.Min,
				BoundingBoxMax = bbox.Max,
				FragmentCount = item.Geometry.FragmentCount
			});
		}
	}
}
