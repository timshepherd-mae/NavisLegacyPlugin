using System;
using System.Collections.Generic;
using System.Linq;
using NavisLegacyPlugin.Models.ExternalSources;
namespace NavisLegacyPlugin.Services.Topology
{
    /// <summary>
    /// Host-independent projection of explicit SOURCE owner groups into the
    /// existing anchor-snapshot shape consumed by topology analysis.
    ///
    /// SOURCE ownership comes only from ExternalSourceRidOwnerGroup.Owner.
    /// No leaf-upward owner recovery result is consulted.
    /// </summary>
    public static class OwnerGroupTopologyPreparation
    {
        public static IReadOnlyList<PreparedOwnerGroupAnchor> Prepare(
            IReadOnlyList<ExternalModelItemSnapshot> sourceItems,
            IReadOnlyList<ExternalSourceRidOwnerGroup> ownerGroups)
        {
            IReadOnlyList<ExternalModelItemSnapshot> items =
                sourceItems ?? new List<ExternalModelItemSnapshot>();
            IReadOnlyList<ExternalSourceRidOwnerGroup> groups =
                ownerGroups ?? new List<ExternalSourceRidOwnerGroup>();
            var prepared = new List<PreparedOwnerGroupAnchor>();
            var usedSourceRows = new HashSet<int>();
            for (int groupIndex = 0;
                groupIndex < groups.Count;
                groupIndex++)
            {
                ExternalSourceRidOwnerGroup group = groups[groupIndex];
                if (!IsUsableGroup(group))
                {
                    continue;
                }
                string groupId = BuildGroupId(groupIndex, group);
                IReadOnlyList<ExternalSourceAnchorSnapshot> anchors =
                    group.Anchors ?? new List<ExternalSourceAnchorSnapshot>();
                for (int anchorIndex = 0;
                    anchorIndex < anchors.Count;
                    anchorIndex++)
                {
                    ExternalSourceAnchorSnapshot anchor = anchors[anchorIndex];
                    if (anchor == null)
                    {
                        continue;
                    }
                    int sourceRowIndex = FindSourceRowIndex(
                        items,
                        anchor,
                        usedSourceRows);
                    if (sourceRowIndex >= 0)
                    {
                        usedSourceRows.Add(sourceRowIndex);
                    }
                    prepared.Add(
                        new PreparedOwnerGroupAnchor(
                            groupIndex,
                            anchorIndex,
                            groupId,
                            sourceRowIndex,
                            group.RidValue.Trim(),
                            CreateAuthoritativeSnapshot(group, anchor)));
                }
            }
            return prepared.AsReadOnly();
        }
        private static bool IsUsableGroup(
            ExternalSourceRidOwnerGroup group)
        {
            return group != null
                && group.Owner != null
                && !string.IsNullOrWhiteSpace(group.RidValue)
                && group.Anchors != null
                && group.Anchors.Count > 0;
        }
        private static string BuildGroupId(
            int groupIndex,
            ExternalSourceRidOwnerGroup group)
        {
            ExternalSourceOwnerSnapshot owner = group.Owner;
            return "OWNER-"
                + groupIndex.ToString("D6")
                + "|"
                + Normalize(owner.InstanceGuid)
                + "|"
                + Normalize(owner.ItemGuid)
                + "|"
                + Normalize(owner.SourceFile)
                + "|"
                + Normalize(owner.DisplayName)
                + "|"
                + Normalize(group.RidValue);
        }
        private static int FindSourceRowIndex(
            IReadOnlyList<ExternalModelItemSnapshot> items,
            ExternalSourceAnchorSnapshot anchor,
            ISet<int> usedSourceRows)
        {
            List<int> candidates = Enumerable.Range(0, items.Count)
                .Where(index => !usedSourceRows.Contains(index))
                .Where(index => SameIdentity(items[index], anchor))
                .ToList();
            if (candidates.Count == 0)
            {
                return -1;
            }
            foreach (int candidate in candidates)
            {
                if (PropertiesEqual(
                    items[candidate] == null
                        ? null
                        : items[candidate].Properties,
                    anchor.Properties))
                {
                    return candidate;
                }
            }
            return candidates[0];
        }
        private static bool SameIdentity(
            ExternalModelItemSnapshot item,
            ExternalSourceAnchorSnapshot anchor)
        {
            if (item == null || anchor == null)
            {
                return false;
            }
            if (!string.IsNullOrWhiteSpace(anchor.InstanceGuid)
                && !string.Equals(
                    Normalize(item.InstanceGuid),
                    Normalize(anchor.InstanceGuid),
                    StringComparison.Ordinal))
            {
                return false;
            }
            return string.Equals(
                Normalize(item.DisplayName),
                Normalize(anchor.DisplayName),
                StringComparison.Ordinal);
        }
        private static bool PropertiesEqual(
            IEnumerable<ExternalPropertySnapshot> first,
            IEnumerable<ExternalPropertySnapshot> second)
        {
            string[] firstValues = PropertySignatures(first).ToArray();
            string[] secondValues = PropertySignatures(second).ToArray();
            return firstValues.SequenceEqual(secondValues, StringComparer.Ordinal);
        }
        private static IEnumerable<string> PropertySignatures(
            IEnumerable<ExternalPropertySnapshot> properties)
        {
            return (properties ?? Enumerable.Empty<ExternalPropertySnapshot>())
                .Where(property => property != null)
                .Select(property =>
                    Normalize(property.Category)
                    + "|"
                    + Normalize(property.Name)
                    + "|"
                    + Normalize(property.Value))
                .OrderBy(value => value, StringComparer.Ordinal);
        }
        private static ExternalModelItemSnapshot CreateAuthoritativeSnapshot(
            ExternalSourceRidOwnerGroup group,
            ExternalSourceAnchorSnapshot anchor)
        {
            int ownerDepth = Math.Max(0, anchor.OwnerDepthFromAnchor);
            var snapshot = new ExternalModelItemSnapshot
            {
                InstanceGuid = anchor.InstanceGuid,
                DisplayName = anchor.DisplayName,
                HasChildren = false,
                HasRidOwner = true,
                RidOwnerDepthFromAnchor = ownerDepth,
                RidOwner = CreateOwnerPathNode(group.Owner, ownerDepth),
                RidOwnerEvidenceStatus = ExternalRidOwnerEvidenceStatus.BranchOwned,
                RidOwnerEvidenceReason =
                    "Explicit SOURCE RID owner supplied by O01 owner-group transport.",
                AnchorRid = group.RidValue,
                RidOwnerRid = group.RidValue,
                RidPropagationBoundaryDepth = -1,
                RecoveredRidOwnerDepthFromAnchor = -1,
                RecoveredRidOwnerReason =
                    "Leaf-upward owner recovery is not authoritative for O02.",
                RecoveredTransparentNodeCount = 0,
                RecoveredSameRidBranchCount = 0,
                RecoveredBoundaryRid = string.Empty
            };
            foreach (ExternalPropertySnapshot property
                in anchor.Properties ?? new List<ExternalPropertySnapshot>())
            {
                if (property == null)
                {
                    continue;
                }
                snapshot.Properties.Add(
                    new ExternalPropertySnapshot
                    {
                        Category = property.Category,
                        Name = property.Name,
                        Value = property.Value
                    });
            }
            foreach (ExternalTopologyPathNodeSnapshot node
                in anchor.OwnerRelativePath
                    ?? new List<ExternalTopologyPathNodeSnapshot>())
            {
                if (node == null)
                {
                    continue;
                }
                ExternalTopologyPathNodeSnapshot clone = CloneNode(node);
                snapshot.AncestorPath.Add(clone);
                snapshot.TopologyPath.Add(CloneNode(node));
            }
            EnsureAnchorAndOwnerPath(snapshot, group, anchor, ownerDepth);
            return snapshot;
        }
        private static void EnsureAnchorAndOwnerPath(
            ExternalModelItemSnapshot snapshot,
            ExternalSourceRidOwnerGroup group,
            ExternalSourceAnchorSnapshot anchor,
            int ownerDepth)
        {
            if (!snapshot.TopologyPath.Any(node => node.DepthFromItem == 0))
            {
                var anchorNode = new ExternalTopologyPathNodeSnapshot
                {
                    DepthFromItem = 0,
                    InstanceGuid = anchor.InstanceGuid,
                    DisplayName = anchor.DisplayName,
                    ItemGuid = ReadProperty(anchor.Properties, "Item", "GUID"),
                    SourceFile = ReadSourceFile(anchor.Properties),
                    HasChildren = false
                };
                snapshot.TopologyPath.Add(anchorNode);
                snapshot.AncestorPath.Add(CloneNode(anchorNode));
            }
            if (!snapshot.TopologyPath.Any(
                node => node.DepthFromItem == ownerDepth))
            {
                ExternalTopologyPathNodeSnapshot ownerNode =
                    CreateOwnerPathNode(group.Owner, ownerDepth);
                snapshot.TopologyPath.Add(ownerNode);
                snapshot.AncestorPath.Add(CloneNode(ownerNode));
            }
            snapshot.TopologyPath = snapshot.TopologyPath
                .Where(node => node != null)
                .OrderBy(node => node.DepthFromItem)
                .ToList();
            snapshot.AncestorPath = snapshot.AncestorPath
                .Where(node => node != null)
                .OrderBy(node => node.DepthFromItem)
                .ToList();
        }
        private static ExternalTopologyPathNodeSnapshot CreateOwnerPathNode(
            ExternalSourceOwnerSnapshot owner,
            int depth)
        {
            return new ExternalTopologyPathNodeSnapshot
            {
                DepthFromItem = depth,
                InstanceGuid = owner == null ? string.Empty : owner.InstanceGuid,
                DisplayName = owner == null ? string.Empty : owner.DisplayName,
                ItemGuid = owner == null ? string.Empty : owner.ItemGuid,
                SourceFile = owner == null ? string.Empty : owner.SourceFile,
                HasChildren = true
            };
        }
        private static ExternalTopologyPathNodeSnapshot CloneNode(
            ExternalTopologyPathNodeSnapshot node)
        {
            return new ExternalTopologyPathNodeSnapshot
            {
                DepthFromItem = node.DepthFromItem,
                InstanceGuid = node.InstanceGuid,
                DisplayName = node.DisplayName,
                ItemGuid = node.ItemGuid,
                SourceFile = node.SourceFile,
                HasChildren = node.HasChildren
            };
        }
        private static string ReadSourceFile(
            IEnumerable<ExternalPropertySnapshot> properties)
        {
            string value = ReadProperty(
                properties,
                "Item",
                "Source File Name");
            return string.IsNullOrWhiteSpace(value)
                ? ReadProperty(properties, "Item", "Source File")
                : value;
        }
        private static string ReadProperty(
            IEnumerable<ExternalPropertySnapshot> properties,
            string category,
            string name)
        {
            ExternalPropertySnapshot property =
                (properties ?? Enumerable.Empty<ExternalPropertySnapshot>())
                    .FirstOrDefault(value =>
                        value != null
                        && string.Equals(
                            value.Category,
                            category,
                            StringComparison.OrdinalIgnoreCase)
                        && string.Equals(
                            value.Name,
                            name,
                            StringComparison.OrdinalIgnoreCase));
            return property == null ? string.Empty : property.Value;
        }
        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Trim().ToUpperInvariant();
        }
    }
    public sealed class PreparedOwnerGroupAnchor
    {
        public PreparedOwnerGroupAnchor(
            int ownerGroupIndex,
            int anchorIndex,
            string groupId,
            int sourceRowIndex,
            string ridValue,
            ExternalModelItemSnapshot snapshot)
        {
            OwnerGroupIndex = ownerGroupIndex;
            AnchorIndex = anchorIndex;
            GroupId = groupId ?? string.Empty;
            SourceRowIndex = sourceRowIndex;
            RidValue = ridValue ?? string.Empty;
            Snapshot = snapshot
                ?? throw new ArgumentNullException("snapshot");
        }
        public int OwnerGroupIndex { get; private set; }
        public int AnchorIndex { get; private set; }
        public string GroupId { get; private set; }
        public int SourceRowIndex { get; private set; }
        public string RidValue { get; private set; }
        public ExternalModelItemSnapshot Snapshot { get; private set; }
    }
}
