using System;
using System.Collections.Generic;
using System.Linq;
using NavisLegacyPlugin.Models.Topology;

namespace NavisLegacyPlugin.Services.Topology
{
    public sealed class TopologyConfidenceClassifier
    {
        private readonly TopologyAlignmentSettings _settings;
        public TopologyConfidenceClassifier(TopologyAlignmentSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException("settings");
            _settings.Validate();
        }

        public TopologyResolution Classify(IEnumerable<TopologyAnchorEvidence> anchors,
            TopologyItemFingerprint sourceOwner, string ridValue, IEnumerable<TopologyPathNode> sourcePath,
            IEnumerable<TopologyCandidateEvidence> candidates)
        {
            List<TopologyAnchorEvidence> anchorList = (anchors ?? Enumerable.Empty<TopologyAnchorEvidence>()).ToList();
            List<TopologyCandidateEvidence> ranked = (candidates ?? Enumerable.Empty<TopologyCandidateEvidence>())
                .OrderByDescending(x => x.Score).ToList();
            if (anchorList.Count == 0 || anchorList.Any(x => x.Status != TopologyAnchorStatus.Matched))
                return Build(TopologyResolutionStatus.Unresolved, "No complete definitive anchor result is available.", false, 0, 0, anchorList, sourceOwner, ridValue, sourcePath, ranked);
            if (ranked.Count == 0)
                return Build(TopologyResolutionStatus.Unresolved, "No TARGET topology candidate was produced.", false, 0, 0, anchorList, sourceOwner, ridValue, sourcePath, ranked);

            int best = ranked[0].Score;
            int next = ranked.Count > 1 ? ranked[1].Score : 0;
            int separation = best - next;
            if (ranked.Count > 1 && separation < _settings.MinimumSeparation)
                return Build(TopologyResolutionStatus.Ambiguous, "The best viable candidates are separated by fewer than the required points.", false, best, next, anchorList, sourceOwner, ridValue, sourcePath, ranked);
            if (best >= _settings.GoodThreshold)
                return Build(TopologyResolutionStatus.Good, "Topology evidence meets the Good threshold and separation gate.", true, best, next, anchorList, sourceOwner, ridValue, sourcePath, ranked);
            if (best >= _settings.VagueThreshold)
                return Build(TopologyResolutionStatus.Vague, "A preferred candidate exists, but its absolute score is below the Good threshold.", false, best, next, anchorList, sourceOwner, ridValue, sourcePath, ranked);
            return Build(TopologyResolutionStatus.Unresolved, "The best candidate score is below the minimum viable threshold.", false, best, next, anchorList, sourceOwner, ridValue, sourcePath, ranked);
        }

        private static TopologyResolution Build(TopologyResolutionStatus status, string reason, bool eligible, int best, int next,
            IEnumerable<TopologyAnchorEvidence> anchors, TopologyItemFingerprint owner, string rid,
            IEnumerable<TopologyPathNode> sourcePath, IEnumerable<TopologyCandidateEvidence> candidates)
        {
            return new TopologyResolution(status, reason, eligible, best, next, anchors, owner, rid, sourcePath, candidates);
        }
    }
}
