using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Autodesk.Navisworks.Api;

namespace NavisLegacyPlugin.Services
{
	public class ModelLookupService
	{
		public class LookupProgressInfo
		{
			public string Stage { get; set; }
			public int ItemsScanned { get; set; }
		}

		private Dictionary<string, ModelItem> _cachedLookup;
		private Document _cachedDocument;

		public async Task<Dictionary<string, ModelItem>> GetOrBuildLookupAsync(
			string tabName,
			string propertyName,
			IProgress<LookupProgressInfo> progress = null)
		{
			var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
			if (doc == null)
				return new Dictionary<string, ModelItem>(StringComparer.OrdinalIgnoreCase);

			// ✅ In-memory cache reuse
			if (_cachedLookup != null && ReferenceEquals(_cachedDocument, doc))
			{
				progress?.Report(new LookupProgressInfo
				{
					Stage = "Using in-memory lookup cache...",
					ItemsScanned = _cachedLookup.Count
				});

				return _cachedLookup;
			}

			// ✅ Build new lookup
			var lookup = new Dictionary<string, ModelItem>(StringComparer.OrdinalIgnoreCase);

			int counter = 0;

			progress?.Report(new LookupProgressInfo
			{
				Stage = "Building Synchro lookup... scanned",
				ItemsScanned = 0
			});

			foreach (Model model in doc.Models)
			{
				foreach (ModelItem item in model.RootItem.DescendantsAndSelf)
				{
					// ✅ Directly access the category (no full scan)
					var cat = item.PropertyCategories
						.FindCategoryByDisplayName(tabName);

					if (cat != null)
					{
						var prop = cat.Properties
							.FirstOrDefault(p => string.Equals(p.DisplayName, propertyName, StringComparison.OrdinalIgnoreCase));

						if (prop != null)
						{
							var value = prop.Value?.ToDisplayString();

							if (!string.IsNullOrWhiteSpace(value) && !lookup.ContainsKey(value))
							{
								lookup[value] = item;
							}
						}
					}

					counter++;

					if (counter % 200 == 0)
					{
						progress?.Report(new LookupProgressInfo
						{
							Stage = "Building Synchro lookup... scanned",
							ItemsScanned = counter
						});

						await System.Windows.Application.Current.Dispatcher.InvokeAsync(
							() => { },
							DispatcherPriority.Background);
					}
				}

				// ✅ yield between models (good for federated files)
				await System.Windows.Application.Current.Dispatcher.InvokeAsync(
					() => { },
					System.Windows.Threading.DispatcherPriority.Background);
			}

			progress?.Report(new LookupProgressInfo
			{
				Stage = "Lookup build complete",
				ItemsScanned = lookup.Count
			});

			_cachedLookup = lookup;
			_cachedDocument = doc;

			return lookup;
		}

		/// <summary>
		/// Optional manual reset (future-proof if needed)
		/// </summary>
		public void ClearCache()
		{
			_cachedLookup = null;
			_cachedDocument = null;
		}
	}
}