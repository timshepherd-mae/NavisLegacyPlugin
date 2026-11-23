using Microsoft.VisualStudio.TestTools.UnitTesting;
using NavisLegacyPlugin.Services.Topology;

namespace NavisLegacyPlugin.Topology.Tests
{
    [TestClass]
    public class TopologyApplicabilityRulesTests
    {
        [TestMethod]
        public void BranchOwnerBelowLeaf_IsApplicable()
        {
            TopologyApplicabilityAssessment result = Assess(
                false, true, 2, true, "RID-1", "ANCHOR");
            Assert.IsTrue(result.IsApplicable);
            Assert.AreEqual(
                TopologyApplicabilityReasonCode.ApplicableBranchOwner,
                result.ReasonCode);
        }

        [TestMethod]
        public void LeafOwnedRid_IsOwnerDepthZero()
        {
            TopologyApplicabilityAssessment result = Assess(
                false, true, 0, false, "RID-1", "ANCHOR");
            Assert.IsFalse(result.IsApplicable);
            Assert.AreEqual(
                TopologyApplicabilityReasonCode.OwnerDepthZero,
                result.ReasonCode);
        }

        [TestMethod]
        public void SourceBranchRow_IsSourceIsBranch()
        {
            TopologyApplicabilityAssessment result = Assess(
                true, true, 0, true, "RID-1", "ANCHOR");
            Assert.AreEqual(
                TopologyApplicabilityReasonCode.SourceIsBranch,
                result.ReasonCode);
        }

        [TestMethod]
        public void MissingRid_IsRidMissing()
        {
            TopologyApplicabilityAssessment result = Assess(
                false, true, 2, true, string.Empty, "ANCHOR");
            Assert.AreEqual(
                TopologyApplicabilityReasonCode.RidMissing,
                result.ReasonCode);
        }

        [TestMethod]
        public void MissingAnchorKey_IsAnchorKeyMissing()
        {
            TopologyApplicabilityAssessment result = Assess(
                false, true, 2, true, "RID-1", string.Empty);
            Assert.AreEqual(
                TopologyApplicabilityReasonCode.AnchorKeyMissing,
                result.ReasonCode);
        }

        [TestMethod]
        public void MissingOwner_IsOwnerNotResolved()
        {
            TopologyApplicabilityAssessment result = Assess(
                false, false, -1, false, "RID-1", "ANCHOR");
            Assert.AreEqual(
                TopologyApplicabilityReasonCode.OwnerNotResolved,
                result.ReasonCode);
        }

        [TestMethod]
        public void NegativeOwnerDepth_IsInconsistent()
        {
            TopologyApplicabilityAssessment result = Assess(
                false, true, -1, true, "RID-1", "ANCHOR");
            Assert.AreEqual(
                TopologyApplicabilityReasonCode.OwnerEvidenceInconsistent,
                result.ReasonCode);
        }

        [TestMethod]
        public void OwnerWithoutChildren_IsOwnerIsLeaf()
        {
            TopologyApplicabilityAssessment result = Assess(
                false, true, 2, false, "RID-1", "ANCHOR");
            Assert.AreEqual(
                TopologyApplicabilityReasonCode.OwnerIsLeaf,
                result.ReasonCode);
        }

        [TestMethod]
        public void ExistingBooleanContract_RemainsCompatible()
        {
            Assert.IsTrue(TopologyApplicabilityRules.IsBranchOwnerRoute(
                false, true, 2, true, "RID-1", "ANCHOR"));
            Assert.IsFalse(TopologyApplicabilityRules.IsBranchOwnerRoute(
                false, true, 0, false, "RID-1", "ANCHOR"));
        }

        private static TopologyApplicabilityAssessment Assess(
            bool sourceHasChildren,
            bool hasRidOwner,
            int ownerDepth,
            bool ownerHasChildren,
            string rid,
            string key)
        {
            return TopologyApplicabilityRules.Assess(
                sourceHasChildren,
                hasRidOwner,
                ownerDepth,
                ownerHasChildren,
                rid,
                key);
        }
    }
}
