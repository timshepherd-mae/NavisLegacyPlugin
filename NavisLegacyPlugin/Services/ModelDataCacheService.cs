using Autodesk.Navisworks.Api;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Windows;
using System.Windows.Threading;
using NavisLegacyPlugin.Services.Lookups;

namespace NavisLegacyPlugin.Services
{
	public class ModelDataCacheService
	{

		[DataContract]
		private class SynchroLookupCacheFile
		{
			[DataMember] public string DocumentPath { get; set; }
			[DataMember] public int ModelItemCount { get; set; }
			[DataMember] public List<SynchroLookupCacheEntry> Entries { get; set; }
		}

		[DataContract]
		private class SynchroLookupCacheEntry
		{
			[DataMember] public string SynchroId { get; set; }
			[DataMember] public string ItemGuid { get; set; }
		}

		private Dictionary<string, ModelItem> _sessionLookup;
		private Document _sessionDocument;

		/// <summary>
		/// Returns a SynchroID -> ModelItem lookup.
		/// Order of preference:
		/// 1) current-session in-memory cache
		/// 2) disk cache in _cache folder beside model file
		/// 3) full rebuild from model property scan
		/// </summary>
		public async System.Threading.Tasks.Task<Dictionary<string, ModelItem>> GetOrBuildSynchroLookupAsync(
			IProgress<ModelLookupService.LookupProgressInfo> progress = null)
		{
			var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
			if (doc == null)
				return new Dictionary<string, ModelItem>(StringComparer.OrdinalIgnoreCase);

			// 1) In-memory cache for current Navis session
			if (_sessionLookup != null && ReferenceEquals(_sessionDocument, doc))
			{
				progress?.Report(new ModelLookupService.LookupProgressInfo
				{
					Stage = "Using in-memory lookup cache...",
					ItemsScanned = _sessionLookup.Count
				});

				return _sessionLookup;
			}

			// 2) If model file is saved, attempt disk-backed cache
			string docPath = TryGetDocumentPath(doc);
			if (!string.IsNullOrWhiteSpace(docPath) && File.Exists(docPath))
			{
				string cacheFilePath = GetCacheFilePath(docPath);

				var cache = TryLoadValidCache(docPath, cacheFilePath, doc);

				if (cache != null)
				{
					var resolved = await ResolveCachedLookupAsync(doc, cache, progress);

					_sessionLookup = resolved;
					_sessionDocument = doc;

					return resolved;
				}
			}

			// 3) Build fresh, then persist if possible
			var built = await BuildLookupFromModelAsync(doc, progress);

			_sessionLookup = built;
			_sessionDocument = doc;

			if (!string.IsNullOrWhiteSpace(docPath) && File.Exists(docPath))
			{
				TrySaveCache(docPath, built, doc);
			}

			return built;
		}

		private async System.Threading.Tasks.Task<Dictionary<string, ModelItem>> BuildLookupFromModelAsync(
			Document doc,
			IProgress<ModelLookupService.LookupProgressInfo> progress)
		{
			var lookup = new Dictionary<string, ModelItem>(StringComparer.OrdinalIgnoreCase);
			int counter = 0;

			progress?.Report(new ModelLookupService.LookupProgressInfo
			{
				Stage = "Building Synchro lookup... scanned",
				ItemsScanned = 0
			});

			foreach (Model model in doc.Models)
			{
				foreach (ModelItem item in model.RootItem.DescendantsAndSelf)
				{
					foreach (PropertyCategory cat in item.PropertyCategories)
					{
						if (!string.Equals(cat.DisplayName, "Synchro", StringComparison.OrdinalIgnoreCase))
							continue;

						foreach (DataProperty prop in cat.Properties)
						{
							if (!string.Equals(prop.DisplayName, "SynchroID", StringComparison.OrdinalIgnoreCase))
								continue;

							var value = prop.Value != null ? prop.Value.ToDisplayString() : null;

							if (!string.IsNullOrWhiteSpace(value) && !lookup.ContainsKey(value))
							{
								lookup[value] = item;
							}
						}
					}

					counter++;

					if (counter % 500 == 0)
					{
						progress?.Report(new ModelLookupService.LookupProgressInfo
						{
							Stage = "Building Synchro lookup... scanned",
							ItemsScanned = counter
						});

						await System.Windows.Application.Current.Dispatcher.InvokeAsync(
							() => { },
							DispatcherPriority.Background);
					}
				}
			}

			progress?.Report(new ModelLookupService.LookupProgressInfo
			{
				Stage = "Lookup build complete",
				ItemsScanned = lookup.Count
			});

			return lookup;
		}

		private async System.Threading.Tasks.Task<Dictionary<string, ModelItem>> ResolveCachedLookupAsync(
			Document doc,
			SynchroLookupCacheFile cache,
			IProgress<ModelLookupService.LookupProgressInfo> progress)
		{
			var lookup = new Dictionary<string, ModelItem>(StringComparer.OrdinalIgnoreCase);

			// GUID -> SynchroID from disk cache
			var guidToSynchro = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			foreach (var entry in cache.Entries)
			{
				if (string.IsNullOrWhiteSpace(entry.ItemGuid) || string.IsNullOrWhiteSpace(entry.SynchroId))
					continue;

				if (!guidToSynchro.ContainsKey(entry.ItemGuid))
					guidToSynchro[entry.ItemGuid] = entry.SynchroId;
			}

			int counter = 0;

			progress?.Report(new ModelLookupService.LookupProgressInfo
			{
				Stage = "Resolving disk cache... scanned",
				ItemsScanned = 0
			});

			foreach (Model model in doc.Models)
			{
				foreach (ModelItem item in model.RootItem.DescendantsAndSelf)
				{
					string guid = item.InstanceGuid.ToString("D");

					if (guidToSynchro.ContainsKey(guid))
					{
						string synchroId = guidToSynchro[guid];

						if (!lookup.ContainsKey(synchroId))
							lookup[synchroId] = item;
					}

					counter++;

					if (counter % 1000 == 0)
					{
						progress?.Report(new ModelLookupService.LookupProgressInfo
						{
							Stage = "Resolving disk cache... scanned",
							ItemsScanned = counter
						});

						await System.Windows.Application.Current.Dispatcher.InvokeAsync(
							() => { },
							DispatcherPriority.Background);
					}
				}
			}

			progress?.Report(new ModelLookupService.LookupProgressInfo
			{
				Stage = "Disk cache resolved",
				ItemsScanned = lookup.Count
			});

			return lookup;
		}

		private string TryGetDocumentPath(Document doc)
		{
			try
			{
				return doc != null ? doc.FileName : null;
			}
			catch
			{
				return null;
			}
		}

		private string GetCacheFilePath(string documentPath)
		{
			string documentFolder = Path.GetDirectoryName(documentPath);
			string cacheFolder = Path.Combine(documentFolder, "_cache");

			if (!Directory.Exists(cacheFolder))
				Directory.CreateDirectory(cacheFolder);

			string baseName = Path.GetFileNameWithoutExtension(documentPath);
			return Path.Combine(cacheFolder, baseName + ".synchroLookupCache.json");
		}

		private SynchroLookupCacheFile TryLoadValidCache(string documentPath, string cacheFilePath, Document doc)
		{
			try
			{
				if (!File.Exists(cacheFilePath))
					return null;

				using (var stream = File.OpenRead(cacheFilePath))
				{
					var serializer = new DataContractJsonSerializer(typeof(SynchroLookupCacheFile));
					var cache = serializer.ReadObject(stream) as SynchroLookupCacheFile;

					if (cache == null)
						return null;

					bool samePath = string.Equals(cache.DocumentPath, documentPath, StringComparison.OrdinalIgnoreCase);
					bool sameItemCount = cache.ModelItemCount == GetModelItemCount(doc);

					if (samePath && sameItemCount)
					{
						System.Diagnostics.Debug.WriteLine("✅ Cache VALID (path + item count)");
						return cache;
					}

					System.Diagnostics.Debug.WriteLine("❌ Cache INVALID");
				}
			}
			catch
			{
				// swallow cache load issues; rebuild instead
			}

			return null;
		}

		private void TrySaveCache(string documentPath, Dictionary<string, ModelItem> lookup, Document doc)
		{
			try
			{
				if (lookup == null || lookup.Count == 0)
					return;

				var cache = new SynchroLookupCacheFile
				{
					DocumentPath = documentPath,
					ModelItemCount = GetModelItemCount(doc),
					Entries = new List<SynchroLookupCacheEntry>()
				};

				foreach (var kvp in lookup)
				{
					if (kvp.Value == null)
						continue;

					cache.Entries.Add(new SynchroLookupCacheEntry
					{
						SynchroId = kvp.Key,
						ItemGuid = kvp.Value.InstanceGuid.ToString("D")
					});
				}

				string cacheFilePath = GetCacheFilePath(documentPath);

				using (var stream = File.Create(cacheFilePath))
				{
					var serializer = new DataContractJsonSerializer(typeof(SynchroLookupCacheFile));
					serializer.WriteObject(stream, cache);
				}
			}
			catch
			{
				// swallow cache save issues; runtime still works without disk cache
			}
		}

		private int GetModelItemCount(Document doc)
		{
			int count = 0;

			if (doc == null)
				return count;

			foreach (Model model in doc.Models)
			{
				foreach (var item in model.RootItem.DescendantsAndSelf)
				{
					count++;
				}
			}

			return count;
		}
	}
}