using System.Collections.Generic;
using System.Threading.Tasks;
using Autodesk.Navisworks.Api;
using NavisLegacyPlugin.Models;

namespace NavisLegacyPlugin.Services.Lookups
{
	public interface ILookupProvider
	{
		Task<Dictionary<string, ModelItem>> BuildLookupAsync(ProgressConfig progress);
	}
}