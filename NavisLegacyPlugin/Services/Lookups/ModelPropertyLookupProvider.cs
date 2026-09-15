using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Autodesk.Navisworks.Api;
using NavisLegacyPlugin.Models;

namespace NavisLegacyPlugin.Services.Lookups
{
    public class ModelPropertyLookupProvider
        : ILookupProvider
    {
        private readonly ModelLookupService _lookupService;

        private readonly LookupConfig _lookupConfig;

        public ModelPropertyLookupProvider(
            ModelLookupService lookupService,
            LookupConfig lookupConfig)
        {
            _lookupService = lookupService
                ?? throw new ArgumentNullException(
                    nameof(lookupService));

            _lookupConfig = lookupConfig
                ?? throw new ArgumentNullException(
                    nameof(lookupConfig));
        }

        public Task<Dictionary<string, ModelItem>>
            BuildLookupAsync(
                ProgressConfig progress)
        {
            return _lookupService.GetOrBuildLookupAsync(
                _lookupConfig.LookupTab,
                _lookupConfig.LookupProperty,
                new Progress<ModelLookupService.LookupProgressInfo>(
                    info =>
                    {
                        progress?.ProgressText?.Report(
                            $"{info.Stage} {info.ItemsScanned}");
                    }));
        }
    }
}
