using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NavisLegacyPlugin.Models.Topology;
using NavisLegacyPlugin.Services.Topology;

namespace NavisLegacyPlugin.Topology.Tests
{
    [TestClass]
    public sealed class OrderedTopologyAlignerTests
    {
        [TestMethod]
        public void Align_ExactPath_ScoresOneHundred()
        {
            TopologyCandidateEvidence result = Align(
                new[] { "Anchor", "Owner", "Parent", "Root" },
                new[] { " anchor ", "OWNER", "Parent", "Root" });

            Assert.AreEqual(100, result.Score);
            Assert.AreEqual(4, result.Alignment.Count);
            Assert.IsTrue(result.Alignment.All(x => x.Disposition == TopologyAlignmentDisposition.Exact));
        }

        [TestMethod]
        public void Align_OwnerRename_RecordsSubstitution()
        {
            TopologyCandidateEvidence result = Align(
                new[] { "Anchor", "Carrots", "D", "E" },
                new[] { "Anchor", "Bananas", "D", "E" });

            Assert.IsTrue(result.Score < 100);
            Assert.IsTrue(result.Alignment.Any(x => x.Disposition == TopologyAlignmentDisposition.Substituted));
        }

        [TestMethod]
        public void Align_InsertedContainer_RecordsInsertion()
        {
            TopologyCandidateEvidence result = Align(
                new[] { "Anchor", "Owner", "Parent" },
                new[] { "Anchor", "Inserted", "Owner", "Parent" });

            Assert.IsTrue(result.Alignment.Any(x => x.Disposition == TopologyAlignmentDisposition.Inserted));
        }

        [TestMethod]
        public void Align_DeletedContainer_RecordsDeletion()
        {
            TopologyCandidateEvidence result = Align(
                new[] { "Anchor", "Deleted", "Owner", "Parent" },
                new[] { "Anchor", "Owner", "Parent" });

            Assert.IsTrue(result.Alignment.Any(x => x.Disposition == TopologyAlignmentDisposition.Deleted));
        }

        [TestMethod]
        public void Align_NullPaths_ReturnsZeroScoreAndNoSteps()
        {
            var aligner = new OrderedTopologyAligner(new TopologyAlignmentSettings());
            var result = aligner.Align(Fingerprint("Candidate"), null, null);
            Assert.AreEqual(0, result.Score);
            Assert.AreEqual(0, result.Alignment.Count);
        }

        private static TopologyCandidateEvidence Align(string[] source, string[] target)
        {
            var aligner = new OrderedTopologyAligner(new TopologyAlignmentSettings());
            return aligner.Align(Fingerprint("Candidate"), Path(source), Path(target));
        }

        private static TopologyPathNode[] Path(string[] names)
        {
            return names.Select((name, index) => new TopologyPathNode(index, name, Fingerprint(name))).ToArray();
        }

        private static TopologyItemFingerprint Fingerprint(string name)
        {
            return new TopologyItemFingerprint(string.Empty, string.Empty, string.Empty, name);
        }
    }
}
