using System;
using System.Data;
using System.Threading.Tasks;
using NavisLegacyPlugin.Services;

public class InMemoryDataSource : IDataSource
{
	private readonly DataTable _table;

	public InMemoryDataSource(DataTable table)
	{
		_table = table;
	}

	public Task<DataTable> GetDataAsync(IProgress<string> progress)
	{
		return Task.FromResult(_table);
	}
}