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
            Assert.IsTrue(TopologyApplicabilityRules.IsBranchOwnerRoute(
                false, true, 2, true, "RID-1", "ANCHOR"));
        }

        [TestMethod]
        public void LeafOwnedRid_IsNotApplicable()
        {
            Assert.IsFalse(TopologyApplicabilityRules.IsBranchOwnerRoute(
                false, true, 0, false, "RID-1", "ANCHOR"));
        }

        [TestMethod]
        public void SourceBranchRow_IsNotApplicableAsAnchor()
        {
            Assert.IsFalse(TopologyApplicabilityRules.IsBranchOwnerRoute(
                true, true, 0, true, "RID-1", "ANCHOR"));
        }

        [TestMethod]
        public void MissingAnchorKey_IsNotApplicable()
        {
            Assert.IsFalse(TopologyApplicabilityRules.IsBranchOwnerRoute(
                false, true, 2, true, "RID-1", string.Empty));
        }

        [TestMethod]
        public void MissingOwnerEvidence_IsNotApplicable()
        {
            Assert.IsFalse(TopologyApplicabilityRules.IsBranchOwnerRoute(
                false, false, -1, false, "RID-1", "ANCHOR"));
        }
    }
}
