using Autodesk.Navisworks.Api;
using NavisLegacyPlugin.Models.Collections;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace NavisLegacyPlugin.Services.Collections
{
    public sealed class ModelItemCollectionResolver
        : IModelItemCollectionResolver
    {
        public CollectionResolutionResult Resolve(
            IEnumerable<ModelItem> rootItems)
        {
            if (rootItems == null)
                throw new ArgumentNullException(nameof(rootItems));

            Dictionary<Guid, ModelItem> allItems =
                new Dictionary<Guid, ModelItem>();

            foreach (ModelItem rootItem in rootItems)
            {
                AddRecursively(rootItem, allItems, 0);
            }

            List<ModelItem> all = allItems.Values.ToList();
            List<ModelItem> branch = new List<ModelItem>();
            List<ModelItem> leaf = new List<ModelItem>();

            foreach (ModelItem item in all)
            {
                if (HasChildren(item))
                    branch.Add(item);
                else
                    leaf.Add(item);
            }

            return new CollectionResolutionResult(
                all.AsReadOnly(),
                branch.AsReadOnly(),
                leaf.AsReadOnly());
        }

        private static void AddRecursively(
            ModelItem item,
            IDictionary<Guid, ModelItem> uniqueItems,
            int depth)
        {
            Debug.WriteLine("[MATCH65A] ADD RECURSE");

            if (item == null)
            {
                Debug.WriteLine("[MATCH65A] NULL ITEM");
                return;
            }

            Guid key = item.InstanceGuid;

            if (uniqueItems.ContainsKey(key))
            {
                Debug.WriteLine(
                    "[MATCH65A] COLLECTION DEDUPE"
                    + " InstanceGuid=" + key.ToString("D")
                    + " Existing=" + uniqueItems[key].DisplayName
                    + " Discarded=" + item.DisplayName);

                return;
            }


            if (uniqueItems.ContainsKey(key))
            {
                return;
            }

            int childCount = item.Children.Count();

            uniqueItems.Add(key, item);

            foreach (ModelItem child in item.Children)
            {
                AddRecursively(child, uniqueItems, depth + 1);
            }
        }

        private static bool HasChildren(ModelItem item)
        {
            foreach (ModelItem child in item.Children)
            {
                return true;
            }

            return false;
        }
    }
}





