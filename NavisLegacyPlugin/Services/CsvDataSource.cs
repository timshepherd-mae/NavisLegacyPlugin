using System;
using System.Data;
using System.Threading.Tasks;

namespace NavisLegacyPlugin.Services
{
	public class CsvDataSource : IDataSource
	{
		private readonly CsvDataService _csvService;
		private readonly string _filePath;
		private readonly int _startRow;
		private readonly string _keyColumn;

		public CsvDataSource(
			CsvDataService csvService,
			string filePath,
			int startRow,
			string keyColumn)
		{
			_csvService = csvService;
			_filePath = filePath;
			_startRow = startRow;
			_keyColumn = keyColumn;
		}

		public Task<DataTable> GetDataAsync(IProgress<string> progressText = null)
		{
			var progress = new Progress<int>(rows =>
			{
				progressText?.Report($"Reading CSV... {rows}");
			});

			var table = _csvService.ReadCsvWithProgress(
				_filePath,
				_startRow,
				_keyColumn,
				progress);

			return Task.FromResult(table);
		}
	}
}
