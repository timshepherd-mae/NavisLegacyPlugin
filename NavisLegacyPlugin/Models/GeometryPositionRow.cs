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

		public double SizeX => MaxX - MinX;
		public double SizeY => MaxY - MinY;
		public double SizeZ => MaxZ - MinZ;

		public double CenX => BoundingBoxMin.X + SizeX / 2;
		public double CenY => BoundingBoxMin.Y + SizeY / 2;
		public double CenZ => BoundingBoxMin.Z + SizeZ / 2;


		public int FragmentCount { get; set; }
	}
}
