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
        {
            DataSource = dataSource;
            MappingStrategy = mappingStrategy;
            LookupProvider = lookupProvider;
            WriteConfig = writeConfig;
            ProgressConfig = progressConfig;
        }

        public IDataSource DataSource { get; }

        public IMappingStrategy MappingStrategy { get; }

        public ILookupProvider LookupProvider { get; }

        public WriteConfig WriteConfig { get; }

        public ProgressConfig ProgressConfig { get; }
    }
}
