using System;
using System.Data;
using System.IO;
using System.Linq;

namespace NavisLegacyPlugin.Services
{
	public class CsvDataService
	{
		public DataTable ReadCsv(string filePath, int startRow, string requiredColumn)
		{
			if (!File.Exists(filePath))
				throw new FileNotFoundException("CSV file not found.", filePath);

			var lines = File.ReadAllLines(filePath);

			if (lines.Length == 0)
				throw new InvalidOperationException("CSV file is empty.");

			var table = new DataTable();

			// ✅ HEADER ROW (with cleanup)
			var headers = lines[0].Split(',');

			string[] cleanHeaders = headers
				.Select(h => h.Trim().Trim('"').Replace("\uFEFF", ""))
				.ToArray();

			foreach (var header in cleanHeaders)
			{
				table.Columns.Add(header);
			}

			// Case-insensitive column handling
			table.CaseSensitive = false;

			// ✅ Find required column index ONCE
			int requiredIndex = Array.FindIndex(cleanHeaders,
				h => string.Equals(h, requiredColumn, StringComparison.OrdinalIgnoreCase));

			if (requiredIndex < 0)
				throw new InvalidOperationException($"Required column '{requiredColumn}' not found in CSV.");

			// ✅ DATA ROWS
			for (int i = startRow - 1; i < lines.Length; i++)
			{
				if (string.IsNullOrWhiteSpace(lines[i]))
					continue;

				var values = lines[i].Split(',');

				// ✅ FILTER: skip rows where required column is empty
				if (requiredIndex >= values.Length ||
					string.IsNullOrWhiteSpace(values[requiredIndex]))
				{
					continue;
				}

				var row = table.NewRow();

				for (int j = 0; j < cleanHeaders.Length && j < values.Length; j++)
				{
					row[j] = values[j].Trim();
				}

				table.Rows.Add(row);
			}

			return table;
		}
	}
}