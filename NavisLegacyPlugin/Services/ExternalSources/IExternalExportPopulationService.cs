using NavisLegacyPlugin.Models.ExternalSources;
using System;
namespace NavisLegacyPlugin.Services.ExternalSources
{
    public interface IExternalExportPopulationService
    {
        ExternalExportPopulationResponse Validate(
            ExternalExportPopulationRequest request,
            IProgress<ExternalSourceProgress> progress = null);
        ExternalExportPopulationResponse Resolve(
            ExternalExportPopulationRequest request,
            IProgress<ExternalSourceProgress> progress = null);
    }
}
