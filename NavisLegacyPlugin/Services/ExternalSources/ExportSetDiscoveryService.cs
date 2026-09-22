using Autodesk.Navisworks.Api;
using NavisLegacyPlugin.Models.ExternalSources;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NavisLegacyPlugin.Services.ExternalSources
{
    /// <summary>
    /// Discovers the direct Selection Set children of the required
    /// MAE-4D/DATA-TRANSFER/EXPORT folder. This service does not resolve
    /// ModelItems and does not participate in transfer execution.
    /// </summary>
    public sealed class ExportSetDiscoveryService : IExportSetDiscoveryService
    {
        private static readonly string[] ExportFolderPath =
        {
            "MAE-4D",
            "DATA-TRANSFER",
            "EXPORT"
        };

        public IReadOnlyList<ExportSetDefinition> Discover(Document document)
        {
            if (document == null)
            {
                throw new InvalidOperationException("No document was supplied.");
            }

            GroupItem exportFolder = FindRequiredFolder(document, ExportFolderPath);
            List<SavedItem> children =
                exportFolder.Children.Cast<SavedItem>().ToList();

            if (children.Count == 0)
            {
                throw new InvalidOperationException(
                    "Selection Set folder 'MAE-4D/DATA-TRANSFER/EXPORT' contains no export sets.");
            }

            List<SavedItem> invalidItems =
                children
                    .Where(x => !(x is SelectionSet))
                    .ToList();

            if (invalidItems.Count > 0)
            {
                throw new InvalidOperationException(
                    string.Format(
                        "Selection Set folder 'MAE-4D/DATA-TRANSFER/EXPORT' must contain only direct Selection Sets. Invalid item(s): {0}.",
                        string.Join(", ", invalidItems.Select(x => x.DisplayName).ToArray())));
            }

            List<SelectionSet> selectionSets =
                children.OfType<SelectionSet>().ToList();

            if (selectionSets.Count == 0)
            {
                throw new InvalidOperationException(
                    "Selection Set folder 'MAE-4D/DATA-TRANSFER/EXPORT' contains no export sets.");
            }

            List<string> duplicateNames =
                selectionSets
                    .GroupBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .Where(x => x.Count() > 1)
                    .Select(x => x.Key)
                    .ToList();

            if (duplicateNames.Count > 0)
            {
                throw new InvalidOperationException(
                    string.Format(
                        "Export set names must be unique (case-insensitive). Duplicate(s): {0}.",
                        string.Join(", ", duplicateNames.ToArray())));
            }

            return selectionSets
                .OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(
                    x => new ExportSetDefinition(
                        x.DisplayName,
                        ExportFolderPath.Concat(new[] { x.DisplayName })))
                .ToList();
        }

        private static GroupItem FindRequiredFolder(
            Document document,
            IEnumerable<string> path)
        {
            string[] parts = path.ToArray();
            SavedItemCollection children = document.SelectionSets.RootItem.Children;
            GroupItem currentFolder = null;

            for (int index = 0; index < parts.Length; index++)
            {
                string part = parts[index];
                List<SavedItem> matches =
                    children
                        .Cast<SavedItem>()
                        .Where(
                            x => string.Equals(
                                x.DisplayName,
                                part,
                                StringComparison.OrdinalIgnoreCase))
                        .ToList();

                if (matches.Count == 0)
                {
                    throw new InvalidOperationException(
                        string.Format(
                            "Required Selection Set folder '{0}' was not found.",
                            string.Join("/", parts.Take(index + 1).ToArray())));
                }

                if (matches.Count > 1)
                {
                    throw new InvalidOperationException(
                        string.Format(
                            "Selection Set path '{0}' is ambiguous because '{1}' exists more than once.",
                            string.Join("/", parts),
                            part));
                }

                currentFolder = matches[0] as GroupItem;
                if (currentFolder == null)
                {
                    throw new InvalidOperationException(
                        string.Format(
                            "'{0}' must be a Selection Set folder.",
                            string.Join("/", parts.Take(index + 1).ToArray())));
                }

                children = currentFolder.Children;
            }

            return currentFolder;
        }
    }
}
