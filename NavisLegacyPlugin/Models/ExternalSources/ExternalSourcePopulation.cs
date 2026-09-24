using System;
using System.Collections.Generic;
using NavisLegacyPlugin.Models.ExternalSources;

namespace NavisLegacyPlugin.Models.ExternalSources
{
    /// <summary>
    /// Host-side, read-only representation of a successfully extracted external SOURCE population.
    /// It deliberately contains no Autodesk Navisworks API objects.
    /// </summary>
    public sealed class ExternalSourcePopulation
    {
        public ExternalSourcePopulation(
            string sourceFile,
            string exportSetName,
            string resolutionType,
            IReadOnlyList<ExternalModelItemSnapshot> items,
            IReadOnlyCollection<Guid> instanceGuids)
        {
            SourceFile = sourceFile;
            ExportSetName = exportSetName;
            ResolutionType = resolutionType;
            Items = items ?? throw new ArgumentNullException("items");
            InstanceGuids = instanceGuids ?? throw new ArgumentNullException("instanceGuids");
        }

        public string SourceFile { get; private set; }
        public string ExportSetName { get; private set; }
        public string ResolutionType { get; private set; }
        public IReadOnlyList<ExternalModelItemSnapshot> Items { get; private set; }
        public IReadOnlyCollection<Guid> InstanceGuids { get; private set; }
    }
}
