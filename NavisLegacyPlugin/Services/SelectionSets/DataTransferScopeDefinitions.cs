using NavisLegacyPlugin.Models.Scopes;

namespace NavisLegacyPlugin.Services.SelectionSets
{
    public static class DataTransferScopeDefinitions
    {
        public static ScopeDefinition CreateSource()
        {
            return new ScopeDefinition(
                TransferScopeType.Source,
                new CurrentDocumentLocation(),
                DataTransferSelectionSetNames.SourcePath);
        }

        public static ScopeDefinition CreateTarget()
        {
            return new ScopeDefinition(
                TransferScopeType.Target,
                new CurrentDocumentLocation(),
                DataTransferSelectionSetNames.TargetPath);
        }

        public static ScopeDefinition CreateExport()
        {
            return new ScopeDefinition(
                TransferScopeType.Export,
                new CurrentDocumentLocation(),
                DataTransferSelectionSetNames.ExportPath);
        }
    }
}
