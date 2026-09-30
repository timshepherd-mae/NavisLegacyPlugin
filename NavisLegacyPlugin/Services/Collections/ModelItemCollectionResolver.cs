using Autodesk.Navisworks.Api;
using NavisLegacyPlugin.Models.Collections;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

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

            HashSet<ModelItem> seenItems = new HashSet<ModelItem>(
                ModelItemReferenceComparer.Instance);
            List<ModelItem> allItems = new List<ModelItem>();

            foreach (ModelItem rootItem in rootItems)
            {
                AddRecursively(rootItem, seenItems, allItems, 0);
            }

            List<ModelItem> all = allItems;
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
            ISet<ModelItem> seenItems,
            IList<ModelItem> allItems,
            int depth)
        {
            if (item == null)
                return;

            // InstanceGuid is a match attribute, not a globally unique ModelItem identity.
            // Federated/instanced models can legitimately expose the same InstanceGuid on
            // different ModelItems. ModelItem equality retains those distinct items while
            // still preventing the same item reached through overlapping roots being added twice.
            if (!seenItems.Add(item))
            {
                Debug.WriteLine(
                    "[MATCH65A] COLLECTION DEDUPE same ModelItem"
                    + " InstanceGuid=" + item.InstanceGuid.ToString("D")
                    + " DisplayName=" + item.DisplayName);
                return;
            }

            allItems.Add(item);

            foreach (ModelItem child in item.Children)
            {
                AddRecursively(child, seenItems, allItems, depth + 1);
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

        private sealed class ModelItemReferenceComparer : IEqualityComparer<ModelItem>
        {
            public static readonly ModelItemReferenceComparer Instance =
                new ModelItemReferenceComparer();

            private ModelItemReferenceComparer()
            {
            }

            public bool Equals(ModelItem x, ModelItem y)
            {
                return object.ReferenceEquals(x, y);
            }

            public int GetHashCode(ModelItem obj)
            {
                return RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}


