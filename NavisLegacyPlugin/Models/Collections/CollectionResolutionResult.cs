using Autodesk.Navisworks.Api;
using System;
using System.Collections.Generic;

namespace NavisLegacyPlugin.Models.Collections
{
    public sealed class CollectionResolutionResult
    {
        public CollectionResolutionResult(
            IReadOnlyCollection<ModelItem> allItems,
            IReadOnlyCollection<ModelItem> branchItems,
            IReadOnlyCollection<ModelItem> leafItems)
        {
            if (allItems == null)
                throw new ArgumentNullException(nameof(allItems));
            if (branchItems == null)
                throw new ArgumentNullException(nameof(branchItems));
            if (leafItems == null)
                throw new ArgumentNullException(nameof(leafItems));

            AllItems = allItems;
            BranchItems = branchItems;
            LeafItems = leafItems;
        }

        public IReadOnlyCollection<ModelItem> AllItems { get; private set; }
        public IReadOnlyCollection<ModelItem> BranchItems { get; private set; }
        public IReadOnlyCollection<ModelItem> LeafItems { get; private set; }

        public IReadOnlyCollection<ModelItem> GetItems(
            CollectionResolutionType resolutionType)
        {
            switch (resolutionType)
            {
                case CollectionResolutionType.All:
                    return AllItems;
                case CollectionResolutionType.Branch:
                    return BranchItems;
                case CollectionResolutionType.Leaf:
                    return LeafItems;
                default:
                    throw new ArgumentOutOfRangeException(nameof(resolutionType));
            }
        }
    }
}
