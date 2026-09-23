using NavisLegacyPlugin.Models.ExternalSources;
using System;
using System.Collections.Generic;
using System.IO;

namespace NavisLegacyPlugin.Services.ExternalSources
{
    /// <summary>
    /// Validates and normalises a user-selected external NWD/NWF path.
    /// This service performs file-system validation only. It never opens a
    /// Navisworks document and never changes Application.ActiveDocument.
    /// </summary>
    public sealed class ExternalSourceFileDefinitionService :
        IExternalSourceFileDefinitionService
    {
        private static readonly HashSet<string> SupportedExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".nwd",
                ".nwf"
            };

        public ExternalSourceFileDefinition CreateRequired(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException(
                    "An external source NWD/NWF file path is required.",
                    "filePath");
            }

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(filePath.Trim());
            }
            catch (Exception exception)
            {
                throw new ArgumentException(
                    "The external source file path is invalid.",
                    "filePath",
                    exception);
            }

            string extension = Path.GetExtension(fullPath);
            if (!SupportedExtensions.Contains(extension))
            {
                throw new InvalidOperationException(
                    string.Format(
                        "External source file '{0}' must be an NWD or NWF file.",
                        fullPath));
            }

            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException(
                    "The external source file was not found.",
                    fullPath);
            }

            FileAttributes attributes;
            try
            {
                attributes = File.GetAttributes(fullPath);
            }
            catch (Exception exception)
            {
                throw new IOException(
                    string.Format(
                        "The external source file '{0}' could not be accessed.",
                        fullPath),
                    exception);
            }

            if ((attributes & FileAttributes.Directory) == FileAttributes.Directory)
            {
                throw new InvalidOperationException(
                    string.Format(
                        "External source path '{0}' identifies a directory, not a file.",
                        fullPath));
            }

            return new ExternalSourceFileDefinition(
                fullPath,
                Path.GetFileName(fullPath),
                extension.ToLowerInvariant());
        }
    }
}
