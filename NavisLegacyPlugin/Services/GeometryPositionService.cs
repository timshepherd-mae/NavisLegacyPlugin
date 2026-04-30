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
				FragmentCount = item.Geometry.FragmentCount,
				Handle = TryGetAcadHandle(item)
			});
		}

		private static string TryGetHandle(ModelItem item)
		{
			if (item?.PropertyCategories == null)
				return null;

			var itemCategory = item.PropertyCategories.FindCategoryByName("Item");
			if (itemCategory == null)
				return null;

			var handleProperty = itemCategory.Properties.FindPropertyByName("Entity Handle");
			if (handleProperty == null || handleProperty.Value == null)
				return null;

			return handleProperty.Value.ToDisplayString();
		}

		private static string TryGetAcadHandle(ModelItem item)
		{
			if (item?.PropertyCategories == null)
				return null;

			var dwgCategory =
				item.PropertyCategories.FindCategoryByName("LcOpDwgEntityAttrib");

			if (dwgCategory == null)
				return null;

			var entityProp =
				dwgCategory.Properties.FindPropertyByName("LcOaNat64AttributeValue");

			if (entityProp?.Value == null)
				return null;

			return entityProp.Value.ToDisplayString();
		}


	}
}
