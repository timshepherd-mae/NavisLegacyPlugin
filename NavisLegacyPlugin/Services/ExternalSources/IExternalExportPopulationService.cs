using NavisLegacyPlugin.Models.ExternalSources;

namespace NavisLegacyPlugin.Services.ExternalSources
{
    public interface IExternalExportPopulationService
    {
        ExternalExportPopulationResponse Resolve(ExternalExportPopulationRequest request);
    }
}
