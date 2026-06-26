using System.Collections.Generic;
using System.Threading.Tasks;
using Autodesk.Navisworks.Api;
using NavisLegacyPlugin.Models;

namespace NavisLegacyPlugin.Services.Lookups
{
	public class DictionaryLookupProvider : ILookupProvider
	{
		private readonly Dictionary<string, ModelItem> _lookup;

		public DictionaryLookupProvider(Dictionary<string, ModelItem> lookup)
		{
			_lookup = lookup;
		}

		public Task<Dictionary<string, ModelItem>> BuildLookupAsync(ProgressConfig progress)
		{
			return Task.FromResult(_lookup);
		}
	}
}