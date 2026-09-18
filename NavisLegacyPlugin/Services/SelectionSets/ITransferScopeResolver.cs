using Autodesk.Navisworks.Api;
using NavisLegacyPlugin.Models.Scopes;

namespace NavisLegacyPlugin.Services.SelectionSets
{
    public interface ITransferScopeResolver
    {
        ScopeResolution Resolve(
            Document document,
            ScopeDefinition definition);
    }
}
