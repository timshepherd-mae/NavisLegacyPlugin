using Autodesk.Navisworks.Api;
using NavisLegacyPlugin.Models.Collections;
using System;
using System.Collections.Generic;
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
                AddRecursively(rootItem, allItems);
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
            IDictionary<Guid, ModelItem> uniqueItems)
        {
            if (item == null)
                return;

            Guid key = item.InstanceGuid;
            if (uniqueItems.ContainsKey(key))
                return;

            uniqueItems.Add(key, item);

            foreach (ModelItem child in item.Children)
            {
                AddRecursively(child, uniqueItems);
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
