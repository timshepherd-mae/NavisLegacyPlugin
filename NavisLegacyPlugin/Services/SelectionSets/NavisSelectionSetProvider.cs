using Autodesk.Navisworks.Api;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NavisLegacyPlugin.Services.SelectionSets
{
    public sealed class NavisSelectionSetProvider
        : INavisSelectionSetProvider
    {
        public IReadOnlyList<SavedItem> FindRootItems(
            Document document,
            string displayName)
        {
            if (document == null)
            {
                throw new ArgumentNullException(
                    "document");
            }

            return document.SelectionSets.RootItem.Children
                .Cast<SavedItem>()
                .Where(
                    x => string.Equals(
                        x.DisplayName,
                        displayName,
                        StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        public IReadOnlyList<SavedItem> FindChildren(
            GroupItem parent,
            string displayName)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(
                    "parent");
            }

            return parent.Children
                .Cast<SavedItem>()
                .Where(
                    x => string.Equals(
                        x.DisplayName,
                        displayName,
                        StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        public IReadOnlyList<ModelItem> GetSelectedItems(
            Document document,
            SelectionSet selectionSet)
        {
            if (document == null)
            {
                throw new ArgumentNullException(
                    "document");
            }

            if (selectionSet == null)
            {
                throw new ArgumentNullException(
                    "selectionSet");
            }

            return selectionSet
                .GetSelectedItems(document)
                .Cast<ModelItem>()
                .GroupBy(
                    x => x.InstanceGuid)
                .Select(
                    x => x.First())
                .ToList();
        }
    }
}
