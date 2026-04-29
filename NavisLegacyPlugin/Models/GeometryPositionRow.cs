using Autodesk.Navisworks.Api;

namespace NavisLegacyPlugin.Models
{
	public class GeometryPositionRow
	{
		public ModelItem ModelItem { get; set; }

		public string ItemGuid { get; set; }

		public Point3D BoundingBoxMin { get; set; }
		public Point3D BoundingBoxMax { get; set; }

		public double MinX => BoundingBoxMin.X;
		public double MinY => BoundingBoxMin.Y;
		public double MinZ => BoundingBoxMin.Z;

		public double MaxX => BoundingBoxMax.X;
		public double MaxY => BoundingBoxMax.Y;
		public double MaxZ => BoundingBoxMax.Z;

		public int FragmentCount { get; set; }
	}
}
