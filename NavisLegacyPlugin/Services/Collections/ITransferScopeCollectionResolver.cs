using Autodesk.Navisworks.Api;
using NavisLegacyPlugin.Models.Collections;
using NavisLegacyPlugin.Models.Scopes;
using System.Collections.Generic;

namespace NavisLegacyPlugin.Services.Collections
{
    public interface ITransferScopeCollectionResolver
    {
        CollectionResolutionResult Resolve(
            ScopeResolution scopeResolution);

        IReadOnlyCollection<ModelItem> Resolve(
            ScopeResolution scopeResolution,
            CollectionResolutionType resolutionType);
    }
}
