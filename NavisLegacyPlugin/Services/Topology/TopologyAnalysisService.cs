using System;
using System.Collections.Generic;
using System.Linq;
using NavisLegacyPlugin.Models.Topology;

namespace NavisLegacyPlugin.Services.Topology
{
    public sealed class TopologyAnalysisService
    {
        private readonly OrderedTopologyAligner _aligner;
        private readonly TopologyConfidenceClassifier _classifier;

        public TopologyAnalysisService(TopologyAlignmentSettings settings)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            _aligner = new OrderedTopologyAligner(settings);
            _classifier = new TopologyConfidenceClassifier(settings);
        }

        public TopologyGroupAnalysis Analyse(TopologySourceGroup source,
            IEnumerable<TopologyTargetCandidate> candidates)
        {
            if (source == null) throw new ArgumentNullException("source");
            List<TopologyTargetCandidate> available = (candidates ?? Enumerable.Empty<TopologyTargetCandidate>()).ToList();
            var evidence = new List<TopologyCandidateEvidence>();
            foreach (TopologyTargetCandidate candidate in available)
            {
                evidence.Add(_aligner.Align(candidate.Fingerprint, source.SourcePath, candidate.Path));
            }
            TopologyResolution resolution = _classifier.Classify(source.Anchors, source.SourceOwner,
                source.RidValue, source.SourcePath, evidence);
            TopologyTargetCandidate selected = null;
            if (resolution.Status == TopologyResolutionStatus.Good && evidence.Count > 0)
            {
                TopologyCandidateEvidence best = evidence.OrderByDescending(x => x.Score).First();
                selected = available.FirstOrDefault(x => FingerprintsEqual(x.Fingerprint, best.Candidate));
            }
            return new TopologyGroupAnalysis(source, resolution, selected);
        }

        private static bool FingerprintsEqual(TopologyItemFingerprint first, TopologyItemFingerprint second)
        {
            if (first == null || second == null) return false;
            return string.Equals(first.InstanceGuid, second.InstanceGuid, StringComparison.OrdinalIgnoreCase)
                && string.Equals(first.ItemGuid, second.ItemGuid, StringComparison.OrdinalIgnoreCase)
                && string.Equals(first.SourceFile, second.SourceFile, StringComparison.OrdinalIgnoreCase)
                && string.Equals(first.DisplayName, second.DisplayName, StringComparison.OrdinalIgnoreCase);
        }
    }
}
