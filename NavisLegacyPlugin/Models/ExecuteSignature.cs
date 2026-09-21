using NavisLegacyPlugin.Services;
using NavisLegacyPlugin.Services.DataSources;
using NavisLegacyPlugin.Services.Lookups;
using NavisLegacyPlugin.Services.Mappers;

namespace NavisLegacyPlugin.Models
{
    public sealed class ExecuteSignature
    {
        public ExecuteSignature(
            IDataSource dataSource,
            IMappingStrategy mappingStrategy,
            ILookupProvider lookupProvider,
            WriteConfig writeConfig,
            ProgressConfig progressConfig)
            : this(
                dataSource,
                mappingStrategy,
                lookupProvider,
                writeConfig,
                progressConfig,
                null)
        {
        }

        public ExecuteSignature(
            IDataSource dataSource,
            IMappingStrategy mappingStrategy,
            ILookupProvider lookupProvider,
            WriteConfig writeConfig,
            ProgressConfig progressConfig,
            ExecutionSelectionSets selectionSets)
        {
            DataSource = dataSource;
            MappingStrategy = mappingStrategy;
            LookupProvider = lookupProvider;
            WriteConfig = writeConfig;
            ProgressConfig = progressConfig;
            SelectionSets = selectionSets;
        }

        public IDataSource DataSource { get; }

        public IMappingStrategy MappingStrategy { get; }

        public ILookupProvider LookupProvider { get; }

        public WriteConfig WriteConfig { get; }

        public ProgressConfig ProgressConfig { get; }

        public ExecutionSelectionSets SelectionSets { get; }
    }
}
