using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Autodesk.Navisworks.Api;
using NavisLegacyPlugin.Models.Collections;
using NavisLegacyPlugin.Models.Scopes;

namespace NavisLegacyPlugin.Services.Collections
{
    public sealed class TransferScopeCollectionResolver
        : ITransferScopeCollectionResolver
    {
        private readonly IModelItemCollectionResolver
            _collectionResolver;

        public TransferScopeCollectionResolver()
            : this(new ModelItemCollectionResolver())
        {
        }

        public TransferScopeCollectionResolver(
            IModelItemCollectionResolver collectionResolver)
        {
            if (collectionResolver == null)
                throw new ArgumentNullException(nameof(collectionResolver));

            _collectionResolver = collectionResolver;
        }

        public CollectionResolutionResult Resolve(
            ScopeResolution scopeResolution)
        {
            if (scopeResolution == null)
                throw new ArgumentNullException(nameof(scopeResolution));


            CollectionResolutionResult result =
                _collectionResolver.Resolve(scopeResolution.Items);


            return result;
        }

        public IReadOnlyCollection<ModelItem> Resolve(
            ScopeResolution scopeResolution,
            CollectionResolutionType resolutionType)
        {
            CollectionResolutionResult result =
                Resolve(scopeResolution);

            IReadOnlyCollection<ModelItem> selectedItems =
                result.GetItems(resolutionType);

            Debug.WriteLine(
                "Requested resolution type: " + resolutionType +
                " | Returned count: " + selectedItems.Count);

            foreach (ModelItem item in selectedItems)
            {
                Debug.WriteLine(
                    "  SELECTED " + resolutionType +
                    " | " + item.DisplayName +
                    " | Guid=" + item.InstanceGuid);
            }

            return selectedItems;
        }
    }
}


