using System.IO;
using System.Runtime.Serialization.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NavisLegacyPlugin.Models.Topology;

namespace NavisLegacyPlugin.Topology.Tests
{
    [TestClass]
    public sealed class TopologySerializationTests
    {
        [TestMethod]
        public void TopologyResolution_DataContractJson_RoundTripsDecisionEvidence()
        {
            var resolution = new TopologyResolution(
                TopologyResolutionStatus.Good, "good", true, 93, 60,
                new[] { new TopologyAnchorEvidence("IfcGuid+elementId", "A|B", TopologyAnchorStatus.Matched, "matched", Fingerprint("S"), Fingerprint("T")) },
                Fingerprint("Owner"), "RID-007",
                new[] { new TopologyPathNode(0, "Anchor", Fingerprint("Anchor")), new TopologyPathNode(1, "Owner", Fingerprint("Owner")) },
                new[] { new TopologyCandidateEvidence(Fingerprint("Candidate"), 93, new TopologyPathNode[0], new TopologyAlignmentStep[0]) });

            var serializer = new DataContractJsonSerializer(typeof(TopologyResolution));
            using (var stream = new MemoryStream())
            {
                serializer.WriteObject(stream, resolution);
                stream.Position = 0;
                var clone = (TopologyResolution)serializer.ReadObject(stream);
                Assert.AreEqual(TopologyResolutionStatus.Good, clone.Status);
                Assert.AreEqual(93, clone.BestScore);
                Assert.AreEqual(60, clone.NextBestScore);
                Assert.AreEqual(33, clone.Separation);
                Assert.AreEqual("RID-007", clone.RidValue);
                Assert.AreEqual(1, clone.Anchors.Count);
                Assert.AreEqual(2, clone.SourcePath.Count);
                Assert.AreEqual(1, clone.Candidates.Count);
            }
        }

        private static TopologyItemFingerprint Fingerprint(string name)
        {
            return new TopologyItemFingerprint(string.Empty, string.Empty, string.Empty, name);
        }
    }
}
