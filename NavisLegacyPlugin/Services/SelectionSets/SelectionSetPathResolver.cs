using Autodesk.Navisworks.Api;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NavisLegacyPlugin.Services.SelectionSets
{
    public sealed class SelectionSetPathResolver
    {
        public IReadOnlyList<ModelItem> ResolveRequired(
            Document document,
            IEnumerable<string> path)
        {
            if (document == null)
            {
                throw new InvalidOperationException(
                    "No active document.");
            }

            if (path == null)
            {
                throw new ArgumentNullException(
                    "path");
            }

            string[] parts =
                path
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToArray();

            if (parts.Length == 0)
            {
                throw new ArgumentException(
                    "A Selection Set path is required.",
                    "path");
            }

            SavedItem current = null;

            SavedItemCollection children =
                document.SelectionSets.RootItem.Children;

            for (int index = 0;
                index < parts.Length;
                index++)
            {
                string part = parts[index];

                if (children == null)
                {
                    throw new InvalidOperationException(
                        string.Format(
                            "'{0}' is not a Selection Set folder.",
                            string.Join(
                                "/",
                                parts.Take(index).ToArray())));
                }

                List<SavedItem> matches =
                    children
                        .Cast<SavedItem>()
                        .Where(
                            x =>
                                string.Equals(
                                    x.DisplayName,
                                    part,
                                    StringComparison.OrdinalIgnoreCase))
                        .ToList();

                if (matches.Count == 0)
                {
                    throw new InvalidOperationException(
                        string.Format(
                            "Selection Set '{0}' was not found.",
                            string.Join("/", parts)));
                }

                if (matches.Count > 1)
                {
                    throw new InvalidOperationException(
                        string.Format(
                            "Selection Set path '{0}' is ambiguous because '{1}' exists more than once.",
                            string.Join("/", parts),
                            part));
                }

                current = matches[0];

                bool isFinalPart =
                    index == parts.Length - 1;

                if (!isFinalPart)
                {
                    GroupItem group =
                        current as GroupItem;

                    if (group == null)
                    {
                        throw new InvalidOperationException(
                            string.Format(
                                "'{0}' must be a Selection Set folder.",
                                string.Join(
                                    "/",
                                    parts.Take(index + 1).ToArray())));
                    }

                    children = group.Children;
                }
            }

            SelectionSet selectionSet =
                current as SelectionSet;

            if (selectionSet == null)
            {
                throw new InvalidOperationException(
                    string.Format(
                        "'{0}' is not a Selection Set.",
                        string.Join("/", parts)));
            }

            List<ModelItem> items =
                selectionSet
                    .GetSelectedItems(document)
                    .Cast<ModelItem>()
                    .GroupBy(x => x.InstanceGuid)
                    .Select(x => x.First())
                    .ToList();

            if (items.Count == 0)
            {
                throw new InvalidOperationException(
                    string.Format(
                        "Selection Set '{0}' contains no items.",
                        string.Join("/", parts)));
            }

            return items;
        }
    }
}
