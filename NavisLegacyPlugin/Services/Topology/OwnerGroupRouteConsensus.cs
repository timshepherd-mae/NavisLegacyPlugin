using System;
using System.Collections.Generic;
using System.Linq;
using NavisLegacyPlugin.Models.Topology;
namespace NavisLegacyPlugin.Services.Topology
{
    /// <summary>
    /// Host-independent assessment of the highest-scored TARGET branch route
    /// proposed by each representative anchor in one explicit owner group.
    /// </summary>
    public static class OwnerGroupRouteConsensus
    {
        public static OwnerGroupRouteConsensusResult Assess(
            IEnumerable<IEnumerable<TopologyCandidateEvidence>>
                candidateSets)
        {
            List<string> anchorRoutes = (
                candidateSets
                ?? Enumerable.Empty<IEnumerable<TopologyCandidateEvidence>>())
                .Select(BestCandidateRouteKey)
                .ToList();
            List<IGrouping<string, string>> proposedRoutes = anchorRoutes
                .Where(route => !string.IsNullOrWhiteSpace(route))
                .GroupBy(route => route, StringComparer.Ordinal)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key, StringComparer.Ordinal)
                .ToList();
            string majorityRoute = proposedRoutes.Count == 0
                ? string.Empty
                : proposedRoutes[0].Key;
            int agreeingCount = proposedRoutes.Count == 0
                ? 0
                : proposedRoutes[0].Count();
            return new OwnerGroupRouteConsensusResult(
                majorityRoute,
                agreeingCount,
                Math.Max(0, anchorRoutes.Count - agreeingCount),
                proposedRoutes.Count,
                proposedRoutes.Count > 1,
                anchorRoutes);
        }
        public static string BestCandidateRouteKey(
            IEnumerable<TopologyCandidateEvidence> candidates)
        {
            TopologyCandidateEvidence best = (
                candidates
                ?? Enumerable.Empty<TopologyCandidateEvidence>())
                .Where(value =>
                    value != null
                    && value.Candidate != null)
                .OrderByDescending(value => value.Score)
                .ThenBy(value =>
                    CandidateRouteKey(value.Candidate),
                    StringComparer.Ordinal)
                .FirstOrDefault();
            return best == null
                ? string.Empty
                : CandidateRouteKey(best.Candidate);
        }
        public static string CandidateRouteKey(
            TopologyItemFingerprint candidate)
        {
            if (candidate == null)
            {
                return string.Empty;
            }
            return ((candidate.InstanceGuid ?? string.Empty).Trim()
                + "|"
                + (candidate.ItemGuid ?? string.Empty).Trim()
                + "|"
                + (candidate.SourceFile ?? string.Empty).Trim()
                + "|"
                + (candidate.DisplayName ?? string.Empty).Trim())
                .ToUpperInvariant();
        }
    }
    public sealed class OwnerGroupRouteConsensusResult
    {
        public OwnerGroupRouteConsensusResult(
            string majorityRoute,
            int agreeingAnchorCount,
            int dissentingAnchorCount,
            int distinctRouteCount,
            bool hasRouteDissent,
            IEnumerable<string> anchorRoutes)
        {
            MajorityRoute = majorityRoute ?? string.Empty;
            AgreeingAnchorCount = agreeingAnchorCount;
            DissentingAnchorCount = dissentingAnchorCount;
            DistinctRouteCount = distinctRouteCount;
            HasRouteDissent = hasRouteDissent;
            AnchorRoutes = (
                anchorRoutes
                ?? Enumerable.Empty<string>())
                .ToList()
                .AsReadOnly();
        }
        public string MajorityRoute { get; private set; }
        public int AgreeingAnchorCount { get; private set; }
        public int DissentingAnchorCount { get; private set; }
        public int DistinctRouteCount { get; private set; }
        public bool HasRouteDissent { get; private set; }
        public IReadOnlyList<string> AnchorRoutes { get; private set; }
    }
}
