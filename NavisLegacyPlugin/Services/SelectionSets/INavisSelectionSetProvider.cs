using Autodesk.Navisworks.Api;
using System.Collections.Generic;

namespace NavisLegacyPlugin.Services.SelectionSets
{
    public interface INavisSelectionSetProvider
    {
        IReadOnlyList<SavedItem> FindRootItems(
            Document document,
            string displayName);

        IReadOnlyList<SavedItem> FindChildren(
            GroupItem parent,
            string displayName);

        IReadOnlyList<ModelItem> GetSelectedItems(
            Document document,
            SelectionSet selectionSet);
    }
}
