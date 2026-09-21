using Autodesk.Navisworks.Api;
using NavisLegacyPlugin.Models.Collections;
using NavisLegacyPlugin.Models.Scopes;
using System;
using System.Collections.Generic;

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

            return _collectionResolver.Resolve(scopeResolution.Items);
        }

        public IReadOnlyCollection<ModelItem> Resolve(
            ScopeResolution scopeResolution,
            CollectionResolutionType resolutionType)
        {
            CollectionResolutionResult result =
                Resolve(scopeResolution);

            return result.GetItems(resolutionType);
        }
    }
}
