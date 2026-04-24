using Autodesk.Navisworks.Api;

namespace NavisLegacyPlugin.Models
{
	public class GeometryPositionRow
	{
		public string ItemGuid { get; set; }

		public Point3D BoundingBoxMin { get; set; }
		public Point3D BoundingBoxMax { get; set; }

		public int FragmentCount { get; set; }
	}
}
