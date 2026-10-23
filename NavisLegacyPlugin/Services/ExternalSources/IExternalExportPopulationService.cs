using NavisLegacyPlugin.Models.ExternalSources;
using System;
namespace NavisLegacyPlugin.Services.ExternalSources
{
    public interface IExternalExportPopulationService
    {
        ExternalExportPopulationResponse Resolve(
            ExternalExportPopulationRequest request,
            IProgress<ExternalSourceProgress> progress = null);
    }
}
