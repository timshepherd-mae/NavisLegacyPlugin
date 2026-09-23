using NavisLegacyPlugin.Models.Collections;

namespace NavisLegacyPlugin.Models.ExternalSources
{
    public sealed class ExternalExportPopulationRequest
    {
        public ExternalExportPopulationRequest(string filePath, string exportSetName, CollectionResolutionType resolutionType)
        {
            FilePath = filePath;
            ExportSetName = exportSetName;
            ResolutionType = resolutionType;
        }
        public string FilePath { get; private set; }
        public string ExportSetName { get; private set; }
        public CollectionResolutionType ResolutionType { get; private set; }
    }
}
