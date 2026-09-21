using Autodesk.Navisworks.Api;
using NavisLegacyPlugin.Models.Collections;
using System.Collections.Generic;

namespace NavisLegacyPlugin.Services.Collections
{
    public interface IModelItemCollectionResolver
    {
        CollectionResolutionResult Resolve(
            IEnumerable<ModelItem> rootItems);
    }
}
