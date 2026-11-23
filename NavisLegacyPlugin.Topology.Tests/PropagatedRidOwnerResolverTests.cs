using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NavisLegacyPlugin.Services.Topology;

namespace NavisLegacyPlugin.Topology.Tests
{
    [TestClass]
    public class PropagatedRidOwnerResolverTests
    {
        [TestMethod]
        public void BlankContainers_AreTransparent()
        {
            PropagatedRidOwnerRecoveryResult result = Resolve(
                Fact(0, false, "RID-1"),
                Fact(1, false, string.Empty),
                Fact(2, false, "\"\""),
                Fact(3, true, "RID-1"));

            Assert.AreEqual(PropagatedRidOwnerRecoveryStatus.BranchOwned, result.Status);
            Assert.AreEqual(3, result.OwnerDepthFromAnchor);
            Assert.AreEqual(2, result.TransparentNodeCount);
        }

        [TestMethod]
        public void HighestSameRidBranchBelowBoundary_IsOwner()
        {
            PropagatedRidOwnerRecoveryResult result = Resolve(
                Fact(0, false, "RID-1"),
                Fact(1, true, "RID-1"),
                Fact(2, false, string.Empty),
                Fact(3, true, "RID-1"),
                Fact(4, true, "RID-2"));

            Assert.AreEqual(PropagatedRidOwnerRecoveryStatus.BranchOwned, result.Status);
            Assert.AreEqual(3, result.OwnerDepthFromAnchor);
            Assert.AreEqual(2, result.SameRidBranchCount);
            Assert.AreEqual(4, result.PropagationBoundaryDepth);
            Assert.AreEqual("RID-2", result.PropagationBoundaryRid);
        }

        [TestMethod]
        public void ConflictingRid_StopsSearchPermanently()
        {
            PropagatedRidOwnerRecoveryResult result = Resolve(
                Fact(0, false, "RID-1"),
                Fact(1, false, string.Empty),
                Fact(2, true, "RID-2"),
                Fact(3, true, "RID-1"));

            Assert.AreEqual(PropagatedRidOwnerRecoveryStatus.LeafOwned, result.Status);
            Assert.AreEqual(0, result.OwnerDepthFromAnchor);
            Assert.AreEqual(2, result.PropagationBoundaryDepth);
            Assert.AreEqual(0, result.SameRidBranchCount);
        }

        [TestMethod]
        public void SameRidAboveConflict_IsIgnored()
        {
            PropagatedRidOwnerRecoveryResult result = Resolve(
                Fact(0, false, "RID-1"),
                Fact(1, true, "RID-1"),
                Fact(2, true, "RID-9"),
                Fact(3, true, "RID-1"));

            Assert.AreEqual(PropagatedRidOwnerRecoveryStatus.BranchOwned, result.Status);
            Assert.AreEqual(1, result.OwnerDepthFromAnchor);
            Assert.AreEqual(2, result.PropagationBoundaryDepth);
        }

        [TestMethod]
        public void AllBlankAncestors_WithNoSameRidBranch_IsLeafOwned()
        {
            PropagatedRidOwnerRecoveryResult result = Resolve(
                Fact(0, false, "RID-1"),
                Fact(1, false, string.Empty),
                Fact(2, true, string.Empty));

            Assert.AreEqual(PropagatedRidOwnerRecoveryStatus.LeafOwned, result.Status);
            Assert.AreEqual(2, result.TransparentNodeCount);
            Assert.AreEqual(-1, result.PropagationBoundaryDepth);
        }

        [TestMethod]
        public void MissingAnchorRid_ReturnsNone()
        {
            PropagatedRidOwnerRecoveryResult result = Resolve(
                Fact(0, false, string.Empty),
                Fact(1, true, "RID-1"));

            Assert.AreEqual(PropagatedRidOwnerRecoveryStatus.None, result.Status);
            Assert.AreEqual(-1, result.OwnerDepthFromAnchor);
        }

        [TestMethod]
        public void NonContiguousDepths_AreInconsistent()
        {
            PropagatedRidOwnerRecoveryResult result = Resolve(
                Fact(0, false, "RID-1"),
                Fact(2, true, "RID-1"));

            Assert.AreEqual(PropagatedRidOwnerRecoveryStatus.Inconsistent, result.Status);
        }

        [TestMethod]
        public void TraversedRegionDepths_IncludeTransparentContainers()
        {
            PropagatedRidOwnerRecoveryResult result = Resolve(
                Fact(0, false, "RID-1"),
                Fact(1, false, string.Empty),
                Fact(2, true, "RID-1"),
                Fact(3, true, "OTHER"));

            CollectionAssert.AreEqual(
                new[] { 0, 1, 2 },
                new List<int>(result.PropagationSegmentDepths));
        }

        private static PropagatedRidOwnerRecoveryResult Resolve(
            params PropagatedRidPathFact[] values)
        {
            return PropagatedRidOwnerResolver.Resolve(values);
        }

        private static PropagatedRidPathFact Fact(
            int depth,
            bool hasChildren,
            string rid)
        {
            return new PropagatedRidPathFact(depth, hasChildren, rid);
        }
    }
}
