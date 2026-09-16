using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Autodesk.Navisworks.Api;
using NavisLegacyPlugin.Models;

namespace NavisLegacyPlugin.Services.Lookups
{
    public sealed class ModelPropertyLookupProvider
        : ILookupProvider
    {
        private readonly ModelLookupService _lookupService;

        private readonly LookupConfig _lookupConfig;

        public ModelPropertyLookupProvider(
            ModelLookupService lookupService,
            LookupConfig lookupConfig)
        {
            if (lookupService == null)
                throw new ArgumentNullException(
                    nameof(lookupService));

            if (lookupConfig == null)
                throw new ArgumentNullException(
                    nameof(lookupConfig));

            if (string.IsNullOrWhiteSpace(
                lookupConfig.LookupTab))
            {
                throw new ArgumentException(
                    "LookupConfig.LookupTab is required.",
                    nameof(lookupConfig));
            }

            if (string.IsNullOrWhiteSpace(
                lookupConfig.LookupProperty))
            {
                throw new ArgumentException(
                    "LookupConfig.LookupProperty is required.",
                    nameof(lookupConfig));
            }

            _lookupService = lookupService;
            _lookupConfig = lookupConfig;
        }

        public Task<Dictionary<string, ModelItem>>
            BuildLookupAsync(
                ProgressConfig progress)
        {
            IProgress<
                ModelLookupService.LookupProgressInfo>
                lookupProgress = null;

            if (progress != null)
            {
                lookupProgress =
                    new Progress<
                        ModelLookupService.LookupProgressInfo>(
                        info =>
                        {
                            if (info == null)
                                return;

                            progress.ProgressText?.Report(
                                $"{info.Stage} " +
                                $"{info.ItemsScanned}");
                        });
            }

            return _lookupService
                .GetOrBuildLookupAsync(
                    _lookupConfig.LookupTab,
                    _lookupConfig.LookupProperty,
                    lookupProgress);
        }
    }
}
