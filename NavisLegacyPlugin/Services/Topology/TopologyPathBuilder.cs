using System;
using System.Collections.Generic;
using Autodesk.Navisworks.Api;
using NavisLegacyPlugin.Models.Topology;

namespace NavisLegacyPlugin.Services.Topology
{
    public sealed class TopologyPathBuilder
    {
        public IReadOnlyList<TopologyPathNode> BuildFromAnchorToAncestors(ModelItem anchor, int maximumNodes)
        {
            if (anchor == null) throw new ArgumentNullException("anchor");
            if (maximumNodes < 1) throw new ArgumentOutOfRangeException("maximumNodes");
            var result = new List<TopologyPathNode>();
            ModelItem current = anchor;
            int depth = 0;
            while (current != null && result.Count < maximumNodes)
            {
                result.Add(new TopologyPathNode(depth, current.DisplayName, CreateFingerprint(current)));
                current = current.Parent;
                depth++;
            }
            return result.AsReadOnly();
        }

        public TopologyItemFingerprint CreateFingerprint(ModelItem item)
        {
            if (item == null) return null;
            return new TopologyItemFingerprint(item.InstanceGuid.ToString("D"),
                ReadProperty(item, "Item", "GUID"), ReadSourceFile(item), item.DisplayName);
        }

        private static string ReadSourceFile(ModelItem item)
        {
            string value = ReadProperty(item, "Item", "Source File Name");
            return string.IsNullOrWhiteSpace(value) ? ReadProperty(item, "Item", "Source File") : value;
        }

        private static string ReadProperty(ModelItem item, string categoryName, string propertyName)
        {
            if (item == null || item.PropertyCategories == null) return string.Empty;
            foreach (PropertyCategory category in item.PropertyCategories)
            {
                if (category == null || !string.Equals(category.DisplayName, categoryName, StringComparison.OrdinalIgnoreCase)) continue;
                foreach (DataProperty property in category.Properties)
                {
                    if (property == null || !string.Equals(property.DisplayName, propertyName, StringComparison.OrdinalIgnoreCase)) continue;
                    try { return property.Value == null ? string.Empty : property.Value.ToDisplayString(); }
                    catch { return string.Empty; }
                }
            }
            return string.Empty;
        }
    }
}
