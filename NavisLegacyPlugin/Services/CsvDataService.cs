using System;
using System.Data;
using System.IO;

namespace NavisLegacyPlugin.Services
{
	public class CsvDataService
	{
		public DataTable ReadCsv(string filePath, int startRow)
		{
			if (!File.Exists(filePath))
				throw new FileNotFoundException("CSV file not found.", filePath);

			var lines = File.ReadAllLines(filePath);

			if (lines.Length == 0)
				throw new InvalidOperationException("CSV file is empty.");

			var table = new DataTable();

			// ✅ HEADER ROW
			var headers = lines[0].Split(',');

			foreach (var header in headers)
			{
				table.Columns.Add(header.Trim());
			}

			// ✅ DATA ROWS
			for (int i = startRow - 1; i < lines.Length; i++)
			{
				if (string.IsNullOrWhiteSpace(lines[i]))
					continue;

				var values = lines[i].Split(',');
				var row = table.NewRow();

				for (int j = 0; j < headers.Length && j < values.Length; j++)
				{
					row[j] = values[j].Trim();
				}

				table.Rows.Add(row);
			}

			return table;
		}
	}
}