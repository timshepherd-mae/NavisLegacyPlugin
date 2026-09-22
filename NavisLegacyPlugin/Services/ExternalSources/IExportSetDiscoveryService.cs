using Autodesk.Navisworks.Api;
using NavisLegacyPlugin.Models.ExternalSources;
using System.Collections.Generic;

namespace NavisLegacyPlugin.Services.ExternalSources
{
    public interface IExportSetDiscoveryService
    {
        IReadOnlyList<ExportSetDefinition> Discover(Document document);
    }
}
