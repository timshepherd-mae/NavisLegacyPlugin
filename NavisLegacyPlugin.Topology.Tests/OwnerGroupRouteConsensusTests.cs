using Microsoft.VisualStudio.TestTools.UnitTesting;
using NavisLegacyPlugin.Models.Topology;
using NavisLegacyPlugin.Services.Topology;
namespace NavisLegacyPlugin.Topology.Tests
{
    [TestClass]
    public class OwnerGroupRouteConsensusTests
    {
        [TestMethod]
        public void SameHighestScoredRoute_Corroborates()
        {
            OwnerGroupRouteConsensusResult result =
                OwnerGroupRouteConsensus.Assess(
                    new[]
                    {
                        new[] { Candidate("A", 90), Candidate("B", 40) },
                        new[] { Candidate("A", 85), Candidate("C", 30) }
                    });
            Assert.AreEqual(2, result.AgreeingAnchorCount);
            Assert.AreEqual(0, result.DissentingAnchorCount);
            Assert.AreEqual(1, result.DistinctRouteCount);
            Assert.IsFalse(result.HasRouteDissent);
            Assert.AreEqual(
                OwnerGroupRouteConsensus.CandidateRouteKey(
                    Fingerprint("A")),
                result.MajorityRoute);
        }
        [TestMethod]
        public void DifferentHighestScoredRoutes_ForceDissent()
        {
            OwnerGroupRouteConsensusResult result =
                OwnerGroupRouteConsensus.Assess(
                    new[]
                    {
                        new[] { Candidate("A", 90) },
                        new[] { Candidate("B", 90) }
                    });
            Assert.AreEqual(1, result.AgreeingAnchorCount);
            Assert.AreEqual(1, result.DissentingAnchorCount);
            Assert.AreEqual(2, result.DistinctRouteCount);
            Assert.IsTrue(result.HasRouteDissent);
        }
        [TestMethod]
        public void MissingCandidate_IsDissentingEvidenceWithoutSecondRoute()
        {
            OwnerGroupRouteConsensusResult result =
                OwnerGroupRouteConsensus.Assess(
                    new[]
                    {
                        new[] { Candidate("A", 90) },
                        new TopologyCandidateEvidence[0]
                    });
            Assert.AreEqual(1, result.AgreeingAnchorCount);
            Assert.AreEqual(1, result.DissentingAnchorCount);
            Assert.AreEqual(1, result.DistinctRouteCount);
            Assert.IsFalse(result.HasRouteDissent);
        }
        [TestMethod]
        public void NoCandidates_ProducesNoMajorityRoute()
        {
            OwnerGroupRouteConsensusResult result =
                OwnerGroupRouteConsensus.Assess(
                    new[]
                    {
                        new TopologyCandidateEvidence[0],
                        new TopologyCandidateEvidence[0]
                    });
            Assert.AreEqual(string.Empty, result.MajorityRoute);
            Assert.AreEqual(0, result.AgreeingAnchorCount);
            Assert.AreEqual(2, result.DissentingAnchorCount);
            Assert.AreEqual(0, result.DistinctRouteCount);
            Assert.IsFalse(result.HasRouteDissent);
        }
        [TestMethod]
        public void EqualScores_UseStableFingerprintOrdering()
        {
            string routeA = OwnerGroupRouteConsensus.CandidateRouteKey(
                Fingerprint("A"));
            Assert.AreEqual(
                routeA,
                OwnerGroupRouteConsensus.BestCandidateRouteKey(
                    new[]
                    {
                        Candidate("B", 80),
                        Candidate("A", 80)
                    }));
        }
        private static TopologyCandidateEvidence Candidate(
            string identity,
            int score)
        {
            return new TopologyCandidateEvidence(
                Fingerprint(identity),
                score,
                new TopologyPathNode[0],
                new TopologyAlignmentStep[0]);
        }
        private static TopologyItemFingerprint Fingerprint(
            string identity)
        {
            return new TopologyItemFingerprint(
                "instance-" + identity,
                "item-" + identity,
                "target.nwc",
                "Owner " + identity);
        }
    }
}
