using System;

namespace NavisLegacyPlugin.Models.ExternalSources
{
    /// <summary>
    /// Immutable, host-independent identity for one external Navisworks source file.
    /// This model does not open a Navisworks document and does not retain host objects.
    /// </summary>
    public sealed class ExternalSourceFileDefinition
    {
        public ExternalSourceFileDefinition(
            string fullPath,
            string displayName,
            string extension)
        {
            if (string.IsNullOrWhiteSpace(fullPath))
            {
                throw new ArgumentException(
                    "An external source file path is required.",
                    "fullPath");
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException(
                    "An external source display name is required.",
                    "displayName");
            }

            if (string.IsNullOrWhiteSpace(extension))
            {
                throw new ArgumentException(
                    "An external source file extension is required.",
                    "extension");
            }

            FullPath = fullPath;
            DisplayName = displayName;
            Extension = extension;
        }

        public string FullPath { get; private set; }

        public string DisplayName { get; private set; }

        public string Extension { get; private set; }
    }
}
