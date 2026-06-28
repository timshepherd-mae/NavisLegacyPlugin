using System;
using System.Data;
using System.Threading.Tasks;

namespace NavisLegacyPlugin.Services.DataSources
{
	public interface IDataSource
	{
		Task<DataTable> GetDataAsync(IProgress<string> progressText = null);
	}
}