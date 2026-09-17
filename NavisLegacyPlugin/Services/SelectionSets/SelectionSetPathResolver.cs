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

            var parts = path.ToArray();

            SavedItem current = null;

            SavedItemCollection children =
                document.SelectionSets.RootItem.Children;

            foreach (var part in parts)
            {
                current = children
                    .Cast<SavedItem>()
                    .FirstOrDefault(x =>
                        string.Equals(
                            x.DisplayName,
                            part,
                            StringComparison.OrdinalIgnoreCase));

                if (current == null)
                {
                    throw new InvalidOperationException(
                        string.Format(
                            "Selection Set '{0}' not found.",
                            string.Join("/", parts)));
                }

                var group = current as GroupItem;

                if (group != null)
                {
                    children = group.Children;
                }
            }

            var selectionSet = current as SelectionSet;

            if (selectionSet == null)
            {
                throw new InvalidOperationException(
                    string.Format(
                        "'{0}' is not a Selection Set.",
                        string.Join("/", parts)));
            }

            var items = selectionSet
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
