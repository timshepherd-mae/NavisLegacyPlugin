using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Autodesk.Navisworks.Api;
using NavisLegacyPlugin.Helpers;
using NavisLegacyPlugin.Models;
using NavisLegacyPlugin.Models.ExternalSources;
using NavisLegacyPlugin.Services.ExternalSources;
using NavisLegacyPlugin.Services.DataSources;
using NavisLegacyPlugin.Services.Execution;
using NavisLegacyPlugin.Services.Lookups;
using NavisLegacyPlugin.Services.Mappers;
using NavisLegacyPlugin.Services.Matching;

namespace NavisLegacyPlugin.Services
{
	public class DataPaintingService
	{
        private readonly ModelLookupService _lookupService;
        private readonly ExecuteSignatureExecutor _executor;

        public DataPaintingService(
            ModelLookupService lookupService,
            ExecuteSignatureExecutor executor)
        {
            _lookupService = lookupService;
            _executor = executor;
        }

        // Synchro Wrapper for ExecuteAsync with LookupConfig
        public async Task<(int matched, int unmatched)> ExecuteAsync(
			IDataSource dataSource,
			MappingConfig mapping,
			LookupConfig lookup,
			WriteConfig writeConfig,
			ProgressConfig progress)
        {

            var signature =
                new ExecuteSignature(
                    dataSource,
                    new MappingConfigStrategy(mapping),
                    new ModelPropertyLookupProvider(_lookupService, lookup),
                    writeConfig,
                    progress);

            var result =
                await _executor.ExecuteAsync(signature);

            return (
                result.Matched,
                result.Unmatched);
        }


        // Direct Lookup Path for ExecuteAsync with Dictionary Lookup
        public Task<(int matched, int unmatched)> ExecuteAsync(
			IDataSource dataSource,
			MappingConfig mapping,
			Dictionary<string, ModelItem> lookup,
			WriteConfig writeConfig,
			ProgressConfig progress)
		{
            return ExecuteAsync(
                dataSource,
                mapping,
                lookup,
                writeConfig,
                progress,
                null);
        }

        // Selection Set-aware direct lookup path.
        // EXPORT is carried as metadata for future external-source workflows;
        // current-document workflows do not require it to be resolved.
        public async Task<(int matched, int unmatched)> ExecuteAsync(
            IDataSource dataSource,
            MappingConfig mapping,
            Dictionary<string, ModelItem> lookup,
            WriteConfig writeConfig,
            ProgressConfig progress,
            ExecutionSelectionSets selectionSets)
        {
            var signature =
                new ExecuteSignature(
                    dataSource,
                    new MappingConfigStrategy(mapping),
                    new DictionaryLookupProvider(lookup),
                    writeConfig,
                    progress,
                    selectionSets);

            var result =
                await _executor.ExecuteAsync(signature);

            return (
                result.Matched,
                result.Unmatched);
        }


        /// <summary>
        /// Phase 6.4 external SOURCE injection path. The existing ExecuteSignature
        /// contract and all existing ExecuteAsync overloads remain unchanged.
        /// </summary>
        public async Task<(int matched, int unmatched, int written, int skipped, int failed)> ExecuteExternalSourceAsync(
            ExternalExportPopulationResponse response,
            MappingConfig mapping,
            Dictionary<string, ModelItem> targetLookup,
            IEnumerable<ModelItem> targetItems,
            WriteConfig writeConfig,
            ProgressConfig progress,
            ExecutionSelectionSets selectionSets)
        {
            var adapter = new ExternalSourcePopulationAdapter();
            ExternalSourcePopulation population = adapter.Adapt(response);
            IDataSource dataSource = new InMemoryDataSource(adapter.ToDataTable(population));

            var signature = new ExecuteSignature(
                dataSource,
                new MappingConfigStrategy(mapping),
                new DictionaryLookupProvider(targetLookup),
                writeConfig,
                progress,
                selectionSets);

            var strategies = new[]
            {
                MatchStrategyFactory.ItemGuid(0),
                MatchStrategyFactory.ItemGuidAndSourceFile(1)
            };

            // Phase 6.5A2: the ordered path must index the complete TARGET
            // population. The legacy GUID lookup remains on ExecuteSignature for
            // compatibility, but must not define the ordered-match population.
            var rowMatchResolver = new OrderedRowMatchResolver(
                targetItems == null
                    ? Enumerable.Empty<ModelItem>()
                    : targetItems,
                strategies);

            ExecuteResult result = await _executor.ExecuteAsync(
                signature,
                population,
                rowMatchResolver);
            return (
                result.Matched,
                result.Unmatched,
                result.Written,
                result.Skipped,
                0);
        }




    }
}



