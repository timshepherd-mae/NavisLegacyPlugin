using System.Collections.Generic;
using System.Data;

namespace NavisLegacyPlugin.Helpers
{
	public static class PropertyMappingHelper
	{
		/// <summary>
		/// Maps a DataRow (CSV data) to a dictionary of Navis property name → value
		/// using a column → property mapping dictionary.
		/// </summary>
		public static Dictionary<string, string> MapRow(
			DataRow row,
			Dictionary<string, string> columnMap)
		{
			var result = new Dictionary<string, string>();

			foreach (var kvp in columnMap)
			{
				string csvColumnName = kvp.Key;
				string propertyName = kvp.Value;

				if (row.Table.Columns.Contains(csvColumnName))
				{
					result[propertyName] = row[csvColumnName]?.ToString();
				}
			}

			return result;
		}
	}
}
