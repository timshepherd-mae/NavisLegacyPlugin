using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Autodesk.Navisworks.Api;
using NavisLegacyPlugin.Helpers;
using NavisLegacyPlugin.Models;
using NavisLegacyPlugin.Services.DataSources;
using NavisLegacyPlugin.Services.Execution;
using NavisLegacyPlugin.Services.Lookups;
using NavisLegacyPlugin.Services.Mappers;

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



    }
}


