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

			var headers = lines[0].Split(',');

			string[] cleanHeaders = headers
				.Select(h => h.Trim().Trim('"').Replace("\uFEFF", ""))
				.ToArray();

			foreach (var header in cleanHeaders)
			{
				table.Columns.Add(header);
			}

			table.CaseSensitive = false;

			int requiredIndex = Array.FindIndex(cleanHeaders,
				h => string.Equals(h, requiredColumn, StringComparison.OrdinalIgnoreCase));

			if (requiredIndex < 0)
				throw new InvalidOperationException($"Required column '{requiredColumn}' not found in CSV.");

			for (int i = startRow - 1; i < lines.Length; i++)
			{
				if (string.IsNullOrWhiteSpace(lines[i]))
					continue;

				var values = lines[i].Split(',');

				if (requiredIndex >= values.Length || string.IsNullOrWhiteSpace(values[requiredIndex]))
					continue;

				var row = table.NewRow();

				for (int j = 0; j < cleanHeaders.Length && j < values.Length; j++)
				{
					row[j] = values[j].Trim();
				}

				table.Rows.Add(row);
			}

			return table;
		}

		public DataTable ReadCsvWithProgress(
			string filePath,
			int startRow,
			string requiredColumn,
			IProgress<int> progress = null)
		{
			if (!File.Exists(filePath))
				throw new FileNotFoundException("CSV file not found.", filePath);

			var table = new DataTable();

			using (var reader = new StreamReader(filePath))
			{
				// ✅ Read header
				string headerLine = reader.ReadLine();
				if (headerLine == null)
					throw new InvalidOperationException("CSV file is empty.");

				var headers = headerLine.Split(',');

				string[] cleanHeaders = headers
					.Select(h => h.Trim().Trim('"').Replace("\uFEFF", ""))
					.ToArray();

				foreach (var header in cleanHeaders)
				{
					table.Columns.Add(header);
				}

				table.CaseSensitive = false;

				int requiredIndex = Array.FindIndex(cleanHeaders,
					h => string.Equals(h, requiredColumn, StringComparison.OrdinalIgnoreCase));

				if (requiredIndex < 0)
					throw new InvalidOperationException($"Required column '{requiredColumn}' not found.");

				int lineIndex = 1;     // account for header
				int rowAdded = 0;

				while (!reader.EndOfStream)
				{
					string line = reader.ReadLine();

					if (string.IsNullOrWhiteSpace(line))
					{
						lineIndex++;
						continue;
					}

					var values = line.Split(',');

					if (lineIndex < startRow)
					{
						lineIndex++;
						continue;
					}

					if (requiredIndex >= values.Length ||
						string.IsNullOrWhiteSpace(values[requiredIndex]))
					{
						lineIndex++;
						continue;
					}

					var row = table.NewRow();

					for (int j = 0; j < cleanHeaders.Length && j < values.Length; j++)
					{
						row[j] = values[j].Trim();
					}

					table.Rows.Add(row);
					rowAdded++;

					// ✅ REPORT PROGRESS EVERY 25 ROWS
					if (rowAdded % 25 == 0)
					{
						progress?.Report(rowAdded);
					}

					lineIndex++;
				}
			}

			progress?.Report(table.Rows.Count);

			return table;
		}


	}
}