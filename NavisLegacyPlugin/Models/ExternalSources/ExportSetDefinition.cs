using System;
using System.Collections.Generic;
using System.Linq;

namespace NavisLegacyPlugin.Models.ExternalSources
{
    /// <summary>
    /// Identifies one directly selectable Selection Set beneath
    /// MAE-4D/DATA-TRANSFER/EXPORT.
    /// </summary>
    public sealed class ExportSetDefinition
    {
        public ExportSetDefinition(
            string name,
            IEnumerable<string> path)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException(
                    "An export set name is required.",
                    "name");
            }

            if (path == null)
            {
                throw new ArgumentNullException("path");
            }

            string[] pathParts =
                path
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToArray();

            if (pathParts.Length == 0)
            {
                throw new ArgumentException(
                    "An export set path is required.",
                    "path");
            }

            Name = name;
            Path = pathParts;
        }

        public string Name { get; private set; }

        public IReadOnlyList<string> Path { get; private set; }
    }
}
