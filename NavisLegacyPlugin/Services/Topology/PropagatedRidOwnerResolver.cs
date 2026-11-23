using System;
using System.Collections.Generic;
using System.Linq;

namespace NavisLegacyPlugin.Services.Topology
{
    public enum PropagatedRidOwnerRecoveryStatus
    {
        None,
        LeafOwned,
        BranchOwned,
        Inconsistent,
        Ambiguous
    }

    public sealed class PropagatedRidPathFact
    {
        public PropagatedRidPathFact(
            int depthFromAnchor,
            bool hasChildren,
            string ridValue)
        {
            DepthFromAnchor = depthFromAnchor;
            HasChildren = hasChildren;
            RidValue = ridValue ?? string.Empty;
        }

        public int DepthFromAnchor { get; private set; }
        public bool HasChildren { get; private set; }
        public string RidValue { get; private set; }
    }

    public sealed class PropagatedRidOwnerRecoveryResult
    {
        public PropagatedRidOwnerRecoveryResult(
            PropagatedRidOwnerRecoveryStatus status,
            string reason,
            int ownerDepthFromAnchor,
            int propagationBoundaryDepth,
            string propagationBoundaryRid,
            string anchorRid,
            string ownerRid,
            int transparentNodeCount,
            int sameRidBranchCount,
            IEnumerable<int> propagationRegionDepths)
        {
            Status = status;
            Reason = reason ?? string.Empty;
            OwnerDepthFromAnchor = ownerDepthFromAnchor;
            PropagationBoundaryDepth = propagationBoundaryDepth;
            PropagationBoundaryRid = propagationBoundaryRid ?? string.Empty;
            AnchorRid = anchorRid ?? string.Empty;
            OwnerRid = ownerRid ?? string.Empty;
            TransparentNodeCount = transparentNodeCount;
            SameRidBranchCount = sameRidBranchCount;
            PropagationSegmentDepths =
                (propagationRegionDepths ?? Enumerable.Empty<int>())
                    .ToList()
                    .AsReadOnly();
        }

        public PropagatedRidOwnerRecoveryStatus Status { get; private set; }
        public string Reason { get; private set; }
        public int OwnerDepthFromAnchor { get; private set; }
        public int PropagationBoundaryDepth { get; private set; }
        public string PropagationBoundaryRid { get; private set; }
        public string AnchorRid { get; private set; }
        public string OwnerRid { get; private set; }
        public int TransparentNodeCount { get; private set; }
        public int SameRidBranchCount { get; private set; }

        // Property name retained for serialization and caller compatibility.
        // In T04-B this contains the complete traversed ownership region,
        // including transparent blank-RID containers.
        public IReadOnlyList<int> PropagationSegmentDepths { get; private set; }
    }

    /// <summary>
    /// T04-B recovery policy.
    /// Blank/non-transferable intermediate nodes are transparent containers.
    /// A different transferable RID is a hard ownership boundary. The highest
    /// branch carrying the anchor RID below that boundary is the owner.
    /// </summary>
    public static class PropagatedRidOwnerResolver
    {
        public static PropagatedRidOwnerRecoveryResult Resolve(
            IEnumerable<PropagatedRidPathFact> pathFacts)
        {
            List<PropagatedRidPathFact> path =
                (pathFacts ?? Enumerable.Empty<PropagatedRidPathFact>())
                    .Where(value => value != null)
                    .OrderBy(value => value.DepthFromAnchor)
                    .ToList();

            if (path.Count == 0 || path[0].DepthFromAnchor != 0)
            {
                return Result(
                    PropagatedRidOwnerRecoveryStatus.None,
                    "No depth-zero SOURCE anchor fact was supplied.",
                    -1,
                    -1,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    0,
                    0,
                    new int[0]);
            }

            if (!HasOrderedUniqueDepths(path))
            {
                return Result(
                    PropagatedRidOwnerRecoveryStatus.Inconsistent,
                    "The SOURCE path contains duplicate, negative or non-contiguous depths.",
                    -1,
                    -1,
                    string.Empty,
                    NormaliseRid(path[0].RidValue),
                    string.Empty,
                    0,
                    0,
                    path.Select(value => value.DepthFromAnchor));
            }

            string anchorRid = NormaliseRid(path[0].RidValue);
            if (!IsTransferableRid(anchorRid))
            {
                return Result(
                    PropagatedRidOwnerRecoveryStatus.None,
                    "The SOURCE anchor has no transferable RID.",
                    -1,
                    FirstAncestorDepth(path),
                    string.Empty,
                    anchorRid,
                    string.Empty,
                    0,
                    0,
                    new[] { 0 });
            }

            var region = new List<PropagatedRidPathFact>();
            var sameRidBranches = new List<PropagatedRidPathFact>();
            int transparentNodeCount = 0;
            int boundaryDepth = -1;
            string boundaryRid = string.Empty;

            foreach (PropagatedRidPathFact fact in path)
            {
                string currentRid = NormaliseRid(fact.RidValue);

                if (!IsTransferableRid(currentRid))
                {
                    region.Add(fact);
                    if (fact.DepthFromAnchor > 0)
                    {
                        transparentNodeCount++;
                    }
                    continue;
                }

                if (RidEquals(currentRid, anchorRid))
                {
                    region.Add(fact);
                    if (fact.HasChildren && fact.DepthFromAnchor > 0)
                    {
                        sameRidBranches.Add(fact);
                    }
                    continue;
                }

                boundaryDepth = fact.DepthFromAnchor;
                boundaryRid = currentRid;
                break;
            }

            if (sameRidBranches.Count == 0)
            {
                return Result(
                    PropagatedRidOwnerRecoveryStatus.LeafOwned,
                    "No branch carrying the anchor RID was found before the first conflicting transferable RID. Blank intermediate containers were treated as transparent.",
                    0,
                    boundaryDepth,
                    boundaryRid,
                    anchorRid,
                    anchorRid,
                    transparentNodeCount,
                    0,
                    region.Select(value => value.DepthFromAnchor));
            }

            int highestDepth = sameRidBranches.Max(
                value => value.DepthFromAnchor);
            List<PropagatedRidPathFact> highestBranches = sameRidBranches
                .Where(value => value.DepthFromAnchor == highestDepth)
                .ToList();

            if (highestBranches.Count != 1)
            {
                return Result(
                    PropagatedRidOwnerRecoveryStatus.Ambiguous,
                    "More than one same-RID branch occupies the highest depth below the ownership boundary.",
                    -1,
                    boundaryDepth,
                    boundaryRid,
                    anchorRid,
                    string.Empty,
                    transparentNodeCount,
                    sameRidBranches.Count,
                    region.Select(value => value.DepthFromAnchor));
            }

            PropagatedRidPathFact owner = highestBranches[0];
            return Result(
                PropagatedRidOwnerRecoveryStatus.BranchOwned,
                "The highest branch carrying the anchor RID below the first conflicting transferable RID was recovered as the SOURCE owner. Blank intermediate containers were treated as transparent.",
                owner.DepthFromAnchor,
                boundaryDepth,
                boundaryRid,
                anchorRid,
                NormaliseRid(owner.RidValue),
                transparentNodeCount,
                sameRidBranches.Count,
                region.Select(value => value.DepthFromAnchor));
        }

        private static bool HasOrderedUniqueDepths(
            IReadOnlyList<PropagatedRidPathFact> path)
        {
            for (int index = 0; index < path.Count; index++)
            {
                if (path[index].DepthFromAnchor != index)
                {
                    return false;
                }
            }

            return true;
        }

        private static PropagatedRidOwnerRecoveryResult Result(
            PropagatedRidOwnerRecoveryStatus status,
            string reason,
            int ownerDepth,
            int boundaryDepth,
            string boundaryRid,
            string anchorRid,
            string ownerRid,
            int transparentNodeCount,
            int sameRidBranchCount,
            IEnumerable<int> depths)
        {
            return new PropagatedRidOwnerRecoveryResult(
                status,
                reason,
                ownerDepth,
                boundaryDepth,
                boundaryRid,
                anchorRid,
                ownerRid,
                transparentNodeCount,
                sameRidBranchCount,
                depths);
        }

        private static int FirstAncestorDepth(
            IEnumerable<PropagatedRidPathFact> path)
        {
            PropagatedRidPathFact first = path
                .Where(value => value.DepthFromAnchor > 0)
                .OrderBy(value => value.DepthFromAnchor)
                .FirstOrDefault();
            return first == null ? -1 : first.DepthFromAnchor;
        }

        private static bool RidEquals(string first, string second)
        {
            return IsTransferableRid(first)
                && IsTransferableRid(second)
                && string.Equals(
                    first,
                    second,
                    StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTransferableRid(string value)
        {
            return !string.IsNullOrWhiteSpace(value)
                && !string.Equals(
                    value.Trim(),
                    "\"\"",
                    StringComparison.Ordinal);
        }

        private static string NormaliseRid(string value)
        {
            return value == null ? string.Empty : value.Trim();
        }
    }
}
