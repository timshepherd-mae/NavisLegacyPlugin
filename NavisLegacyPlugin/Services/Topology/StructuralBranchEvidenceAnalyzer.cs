using System;
using System.Collections.Generic;
using System.Linq;

namespace NavisLegacyPlugin.Services.Topology
{
    public sealed class StructuralBranchEvidenceRow
    {
        public StructuralBranchEvidenceRow(
            int sourceRow,
            string leafRid,
            int depthFromAnchor,
            string branchKey,
            string branchName,
            string branchRid)
        {
            SourceRow = sourceRow;
            LeafRid = Normalise(leafRid);
            DepthFromAnchor = depthFromAnchor;
            BranchKey = branchKey ?? string.Empty;
            BranchName = branchName ?? string.Empty;
            BranchRid = Normalise(branchRid);
        }
        public int SourceRow { get; private set; }
        public string LeafRid { get; private set; }
        public int DepthFromAnchor { get; private set; }
        public string BranchKey { get; private set; }
        public string BranchName { get; private set; }
        public string BranchRid { get; private set; }
        private static string Normalise(string value) { return value == null ? string.Empty : value.Trim(); }
    }

    public sealed class StructuralBranchCluster
    {
        public StructuralBranchCluster(
            string branchKey,
            string branchName,
            int minimumDepth,
            int maximumDepth,
            int supportingLeafCount,
            int distinctLeafRidCount,
            int blankLeafRidCount,
            int branchRidMatchCount,
            IEnumerable<string> leafRids)
        {
            BranchKey = branchKey ?? string.Empty;
            BranchName = branchName ?? string.Empty;
            MinimumDepth = minimumDepth;
            MaximumDepth = maximumDepth;
            SupportingLeafCount = supportingLeafCount;
            DistinctLeafRidCount = distinctLeafRidCount;
            BlankLeafRidCount = blankLeafRidCount;
            BranchRidMatchCount = branchRidMatchCount;
            LeafRids = (leafRids ?? Enumerable.Empty<string>()).ToList().AsReadOnly();
        }
        public string BranchKey { get; private set; }
        public string BranchName { get; private set; }
        public int MinimumDepth { get; private set; }
        public int MaximumDepth { get; private set; }
        public int SupportingLeafCount { get; private set; }
        public int DistinctLeafRidCount { get; private set; }
        public int BlankLeafRidCount { get; private set; }
        public int BranchRidMatchCount { get; private set; }
        public IReadOnlyList<string> LeafRids { get; private set; }

        public bool IsHomogeneous
        {
            get { return DistinctLeafRidCount == 1 && LeafRids.Count == 1; }
        }

        public string HomogeneousRid
        {
            get { return IsHomogeneous ? LeafRids[0] : string.Empty; }
        }
    }

    public sealed class StructuralOwnerSelection
    {
        public StructuralOwnerSelection(int sourceRow, string rid, string branchKey, string branchName, int ownerDepthFromAnchor, int supportingLeafCount, int candidateCount, string reason)
        {
            SourceRow = sourceRow; Rid = rid ?? string.Empty; BranchKey = branchKey ?? string.Empty; BranchName = branchName ?? string.Empty; OwnerDepthFromAnchor = ownerDepthFromAnchor; SupportingLeafCount = supportingLeafCount; CandidateCount = candidateCount; Reason = reason ?? string.Empty;
        }
        public int SourceRow { get; private set; }
        public string Rid { get; private set; }
        public string BranchKey { get; private set; }
        public string BranchName { get; private set; }
        public int OwnerDepthFromAnchor { get; private set; }
        public int SupportingLeafCount { get; private set; }
        public int CandidateCount { get; private set; }
        public string Reason { get; private set; }
    }

    public sealed class StructuralOwnerSelectionResult
    {
        public StructuralOwnerSelectionResult(IEnumerable<StructuralBranchCluster> clusters, IEnumerable<StructuralOwnerSelection> selections, int noCandidateRowCount, int ambiguousNearestRowCount)
        {
            Clusters = (clusters ?? Enumerable.Empty<StructuralBranchCluster>()).ToList().AsReadOnly();
            Selections = (selections ?? Enumerable.Empty<StructuralOwnerSelection>()).ToList().AsReadOnly();
            NoCandidateRowCount = noCandidateRowCount; AmbiguousNearestRowCount = ambiguousNearestRowCount;
        }
        public IReadOnlyList<StructuralBranchCluster> Clusters { get; private set; }
        public IReadOnlyList<StructuralOwnerSelection> Selections { get; private set; }
        public int NoCandidateRowCount { get; private set; }
        public int AmbiguousNearestRowCount { get; private set; }
    }

    /// <summary>
    /// Diagnostic only. Aggregates RID-bearing leaves by ancestor branch
    /// fingerprint without selecting an owner or changing topology execution.
    /// </summary>
    public static class StructuralBranchEvidenceAnalyzer
    {
        public static IReadOnlyList<StructuralBranchCluster> Analyse(IEnumerable<StructuralBranchEvidenceRow> rows)
        {
            return AnalyseAndSelect(rows).Clusters;
        }

        public static StructuralOwnerSelectionResult AnalyseAndSelect(IEnumerable<StructuralBranchEvidenceRow> rows)
        {
            List<StructuralBranchEvidenceRow> evidence = (rows ?? Enumerable.Empty<StructuralBranchEvidenceRow>()).Where(row => row != null).ToList();
            List<StructuralBranchCluster> clusters = evidence.Where(row => !string.IsNullOrWhiteSpace(row.BranchKey)).GroupBy(row => row.BranchKey, StringComparer.OrdinalIgnoreCase).Select(BuildCluster).OrderByDescending(cluster => cluster.SupportingLeafCount).ThenBy(cluster => cluster.BranchName, StringComparer.OrdinalIgnoreCase).ToList();
            Dictionary<string, StructuralBranchCluster> byKey = clusters.ToDictionary(cluster => cluster.BranchKey, cluster => cluster, StringComparer.OrdinalIgnoreCase);
            var selections = new List<StructuralOwnerSelection>(); int none = 0; int ambiguous = 0;
            foreach (IGrouping<int, StructuralBranchEvidenceRow> sourceGroup in evidence.GroupBy(row => row.SourceRow))
            {
                string rid = sourceGroup.Select(row => row.LeafRid).FirstOrDefault(IsTransferableRid) ?? string.Empty;
                List<StructuralBranchEvidenceRow> candidates = sourceGroup.Where(row => byKey.ContainsKey(row.BranchKey)).Where(row => byKey[row.BranchKey].IsHomogeneous && RidEquals(byKey[row.BranchKey].HomogeneousRid, rid)).OrderBy(row => row.DepthFromAnchor).ThenBy(row => row.BranchKey, StringComparer.OrdinalIgnoreCase).ToList();
                if (candidates.Count == 0) { none++; continue; }
                int depth = candidates[0].DepthFromAnchor;
                List<StructuralBranchEvidenceRow> nearest = candidates.Where(row => row.DepthFromAnchor == depth).GroupBy(row => row.BranchKey, StringComparer.OrdinalIgnoreCase).Select(group => group.First()).ToList();
                if (nearest.Count != 1) { ambiguous++; continue; }
                StructuralBranchEvidenceRow selected = nearest[0]; StructuralBranchCluster cluster = byKey[selected.BranchKey];
                selections.Add(new StructuralOwnerSelection(sourceGroup.Key, rid, selected.BranchKey, selected.BranchName, selected.DepthFromAnchor, cluster.SupportingLeafCount, candidates.Select(row => row.BranchKey).Distinct(StringComparer.OrdinalIgnoreCase).Count(), "Nearest homogeneous ancestor branch for the SOURCE leaf RID."));
            }
            return new StructuralOwnerSelectionResult(clusters, selections, none, ambiguous);
        }

        private static bool RidEquals(string first, string second)
        {
            return IsTransferableRid(first) && IsTransferableRid(second) && string.Equals(first.Trim(), second.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static StructuralBranchCluster BuildCluster(
            IGrouping<string, StructuralBranchEvidenceRow> group)
        {
            List<StructuralBranchEvidenceRow> values = group.ToList();
            List<string> transferableLeafRids = values
                .Select(value => value.LeafRid)
                .Where(IsTransferableRid)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToList();

            int matches = values.Count(value =>
                IsTransferableRid(value.LeafRid)
                && IsTransferableRid(value.BranchRid)
                && string.Equals(value.LeafRid, value.BranchRid, StringComparison.OrdinalIgnoreCase));

            return new StructuralBranchCluster(
                group.Key,
                values.Select(value => value.BranchName).FirstOrDefault() ?? string.Empty,
                values.Min(value => value.DepthFromAnchor),
                values.Max(value => value.DepthFromAnchor),
                values.Select(value => value.SourceRow).Distinct().Count(),
                transferableLeafRids.Count,
                values.Count(value => !IsTransferableRid(value.LeafRid)),
                matches,
                transferableLeafRids);
        }

        private static bool IsTransferableRid(string value)
        {
            return !string.IsNullOrWhiteSpace(value)
                && !string.Equals(value.Trim(), "\"\"", StringComparison.Ordinal);
        }
    }
}
