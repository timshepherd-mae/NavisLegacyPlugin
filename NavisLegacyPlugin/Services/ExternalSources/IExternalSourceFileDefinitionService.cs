using NavisLegacyPlugin.Models.ExternalSources;

namespace NavisLegacyPlugin.Services.ExternalSources
{
    public interface IExternalSourceFileDefinitionService
    {
        ExternalSourceFileDefinition CreateRequired(string filePath);
    }
}
