using Microsoft.VisualStudio.TestTools.UnitTesting;
using NavisLegacyPlugin.Models.Topology;
using NavisLegacyPlugin.Services.Topology;

namespace NavisLegacyPlugin.Topology.Tests
{
    [TestClass]
    public sealed class TopologyConfidenceClassifierTests
    {
        [TestMethod]
        public void Classify_Good_WhenScoreAtLeast80AndSeparatedBy15()
        {
            TopologyResolution result = Classify(80, 65);
            Assert.AreEqual(TopologyResolutionStatus.Good, result.Status);
            Assert.IsTrue(result.WriteEligible);
            Assert.AreEqual(15, result.Separation);
        }

        [TestMethod]
        public void Classify_Vague_WhenScoreBetween50And79AndSeparated()
        {
            TopologyResolution result = Classify(79, 60);
            Assert.AreEqual(TopologyResolutionStatus.Vague, result.Status);
            Assert.IsFalse(result.WriteEligible);
        }

        [TestMethod]
        public void Classify_Ambiguous_WhenViableCandidatesAreTooClose()
        {
            TopologyResolution result = Classify(95, 81);
            Assert.AreEqual(TopologyResolutionStatus.Ambiguous, result.Status);
            Assert.IsFalse(result.WriteEligible);
            Assert.AreEqual(14, result.Separation);
        }

        [TestMethod]
        public void Classify_Unresolved_WhenBestScoreBelow50()
        {
            TopologyResolution result = Classify(49, 0);
            Assert.AreEqual(TopologyResolutionStatus.Unresolved, result.Status);
            Assert.IsFalse(result.WriteEligible);
        }

        [TestMethod]
        public void Classify_Unresolved_WhenAnchorMissingOrNotMatched()
        {
            var classifier = new TopologyConfidenceClassifier(new TopologyAlignmentSettings());
            var result = classifier.Classify(
                new[] { Anchor(TopologyAnchorStatus.MissingKey) },
                Fingerprint("Owner"), "RID-1", new TopologyPathNode[0],
                new[] { Candidate(100) });
            Assert.AreEqual(TopologyResolutionStatus.Unresolved, result.Status);
            Assert.IsFalse(result.WriteEligible);
        }

        [TestMethod]
        public void Classify_Unresolved_WhenNoCandidateExists()
        {
            var classifier = new TopologyConfidenceClassifier(new TopologyAlignmentSettings());
            var result = classifier.Classify(
                new[] { Anchor(TopologyAnchorStatus.Matched) },
                Fingerprint("Owner"), "RID-1", new TopologyPathNode[0],
                new TopologyCandidateEvidence[0]);
            Assert.AreEqual(TopologyResolutionStatus.Unresolved, result.Status);
        }

        private static TopologyResolution Classify(int best, int next)
        {
            var classifier = new TopologyConfidenceClassifier(new TopologyAlignmentSettings());
            return classifier.Classify(
                new[] { Anchor(TopologyAnchorStatus.Matched) },
                Fingerprint("Owner"), "RID-1", new TopologyPathNode[0],
                next > 0 ? new[] { Candidate(best), Candidate(next) } : new[] { Candidate(best) });
        }

        private static TopologyAnchorEvidence Anchor(TopologyAnchorStatus status)
        {
            return new TopologyAnchorEvidence("TestAnchor", "KEY", status, "test", Fingerprint("Source"), Fingerprint("Target"));
        }

        private static TopologyCandidateEvidence Candidate(int score)
        {
            return new TopologyCandidateEvidence(Fingerprint("Candidate" + score), score, new TopologyPathNode[0], new TopologyAlignmentStep[0]);
        }

        private static TopologyItemFingerprint Fingerprint(string name)
        {
            return new TopologyItemFingerprint(string.Empty, string.Empty, string.Empty, name);
        }
    }
}
