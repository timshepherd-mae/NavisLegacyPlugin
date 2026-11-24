using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NavisLegacyPlugin.Models.ExternalSources;
using NavisLegacyPlugin.Services.Topology;
namespace NavisLegacyPlugin.Topology.Tests
{
    [TestClass]
    public class OwnerGroupTopologyPreparationTests
    {
        [TestMethod]
        public void ExplicitOwnerGroup_PreparesAuthoritativeBranchOwner()
        {
            ExternalModelItemSnapshot item = Item(
                "anchor-1",
                "Leaf [100]",
                "IFC-100");
            ExternalSourceRidOwnerGroup group = Group(
                "owner-1",
                "Owner A",
                "RID-100",
                Anchor("anchor-1", "Leaf [100]", 2, "IFC-100"));
            PreparedOwnerGroupAnchor prepared =
                OwnerGroupTopologyPreparation.Prepare(
                    new[] { item },
                    new[] { group })
                    .Single();
            Assert.AreEqual(0, prepared.SourceRowIndex);
            Assert.AreEqual("RID-100", prepared.RidValue);
            Assert.IsTrue(prepared.Snapshot.HasRidOwner);
            Assert.AreEqual(2, prepared.Snapshot.RidOwnerDepthFromAnchor);
            Assert.AreEqual("owner-1", prepared.Snapshot.RidOwner.InstanceGuid);
            Assert.AreEqual("Owner A", prepared.Snapshot.RidOwner.DisplayName);
            Assert.IsTrue(prepared.Snapshot.RidOwner.HasChildren);
            Assert.AreEqual(
                ExternalRidOwnerEvidenceStatus.BranchOwned,
                prepared.Snapshot.RidOwnerEvidenceStatus);
        }
        [TestMethod]
        public void ExplicitOwnerGroup_IgnoresLeafUpwardRecoveryEvidence()
        {
            ExternalModelItemSnapshot item = Item(
                "anchor-1",
                "Leaf [100]",
                "IFC-100");
            item.HasRidOwner = true;
            item.RidOwnerDepthFromAnchor = 9;
            item.RidOwner = new ExternalTopologyPathNodeSnapshot
            {
                DepthFromItem = 9,
                InstanceGuid = "obsolete-owner",
                DisplayName = "Obsolete recovered owner",
                HasChildren = true
            };
            ExternalSourceRidOwnerGroup group = Group(
                "explicit-owner",
                "Explicit owner",
                "RID-100",
                Anchor("anchor-1", "Leaf [100]", 2, "IFC-100"));
            PreparedOwnerGroupAnchor prepared =
                OwnerGroupTopologyPreparation.Prepare(
                    new[] { item },
                    new[] { group })
                    .Single();
            Assert.AreEqual(2, prepared.Snapshot.RidOwnerDepthFromAnchor);
            Assert.AreEqual(
                "explicit-owner",
                prepared.Snapshot.RidOwner.InstanceGuid);
            Assert.AreNotEqual(
                "obsolete-owner",
                prepared.Snapshot.RidOwner.InstanceGuid);
        }
        [TestMethod]
        public void DuplicateTransportIdentity_ConsumesDistinctSourceRows()
        {
            ExternalModelItemSnapshot first = Item(
                "duplicate-guid",
                "Leaf [100]",
                "IFC-100");
            ExternalModelItemSnapshot second = Item(
                "duplicate-guid",
                "Leaf [100]",
                "IFC-100");
            ExternalSourceRidOwnerGroup firstGroup = Group(
                "owner-1",
                "Owner 1",
                "RID-1",
                Anchor("duplicate-guid", "Leaf [100]", 1, "IFC-100"));
            ExternalSourceRidOwnerGroup secondGroup = Group(
                "owner-2",
                "Owner 2",
                "RID-2",
                Anchor("duplicate-guid", "Leaf [100]", 1, "IFC-100"));
            IReadOnlyList<PreparedOwnerGroupAnchor> prepared =
                OwnerGroupTopologyPreparation.Prepare(
                    new[] { first, second },
                    new[] { firstGroup, secondGroup });
            CollectionAssert.AreEquivalent(
                new[] { 0, 1 },
                prepared.Select(value => value.SourceRowIndex).ToArray());
            Assert.AreNotEqual(
                prepared[0].SourceRowIndex,
                prepared[1].SourceRowIndex);
        }
        [TestMethod]
        public void OwnerRelativePath_RoundTripsIntoAuthoritativeTopologyPath()
        {
            ExternalSourceAnchorSnapshot anchor = Anchor(
                "anchor-1",
                "Leaf [100]",
                2,
                "IFC-100");
            anchor.OwnerRelativePath.Add(
                Node(0, "anchor-1", "Leaf [100]", false));
            anchor.OwnerRelativePath.Add(
                Node(1, "container-1", "Container", true));
            anchor.OwnerRelativePath.Add(
                Node(2, "owner-1", "Owner A", true));
            PreparedOwnerGroupAnchor prepared =
                OwnerGroupTopologyPreparation.Prepare(
                    new[] { Item("anchor-1", "Leaf [100]", "IFC-100") },
                    new[] { Group("owner-1", "Owner A", "RID-100", anchor) })
                    .Single();
            CollectionAssert.AreEqual(
                new[] { 0, 1, 2 },
                prepared.Snapshot.TopologyPath
                    .Select(node => node.DepthFromItem)
                    .ToArray());
            Assert.AreEqual(
                "Container",
                prepared.Snapshot.TopologyPath[1].DisplayName);
            Assert.AreEqual(
                "Owner A",
                prepared.Snapshot.TopologyPath[2].DisplayName);
        }
        [TestMethod]
        public void UncorrelatedAnchor_RemainsVisibleWithMinusOneRowIndex()
        {
            ExternalSourceRidOwnerGroup group = Group(
                "owner-1",
                "Owner A",
                "RID-100",
                Anchor("missing-anchor", "Missing leaf", 1, "IFC-404"));
            PreparedOwnerGroupAnchor prepared =
                OwnerGroupTopologyPreparation.Prepare(
                    new ExternalModelItemSnapshot[0],
                    new[] { group })
                    .Single();
            Assert.AreEqual(-1, prepared.SourceRowIndex);
            Assert.AreEqual("missing-anchor", prepared.Snapshot.InstanceGuid);
            Assert.IsTrue(prepared.Snapshot.HasRidOwner);
        }
        private static ExternalModelItemSnapshot Item(
            string instanceGuid,
            string displayName,
            string ifcGuid)
        {
            var item = new ExternalModelItemSnapshot
            {
                InstanceGuid = instanceGuid,
                DisplayName = displayName,
                HasChildren = false
            };
            item.Properties.Add(Property("IFC", "IfcGuid", ifcGuid));
            return item;
        }
        private static ExternalSourceRidOwnerGroup Group(
            string ownerGuid,
            string ownerName,
            string rid,
            ExternalSourceAnchorSnapshot anchor)
        {
            var group = new ExternalSourceRidOwnerGroup
            {
                Owner = new ExternalSourceOwnerSnapshot
                {
                    InstanceGuid = ownerGuid,
                    DisplayName = ownerName,
                    ItemGuid = "item-" + ownerGuid,
                    SourceFile = "source.nwc",
                    RidValue = rid
                },
                RidValue = rid
            };
            group.Anchors.Add(anchor);
            return group;
        }
        private static ExternalSourceAnchorSnapshot Anchor(
            string instanceGuid,
            string displayName,
            int depth,
            string ifcGuid)
        {
            var anchor = new ExternalSourceAnchorSnapshot
            {
                InstanceGuid = instanceGuid,
                DisplayName = displayName,
                OwnerDepthFromAnchor = depth
            };
            anchor.Properties.Add(Property("IFC", "IfcGuid", ifcGuid));
            return anchor;
        }
        private static ExternalPropertySnapshot Property(
            string category,
            string name,
            string value)
        {
            return new ExternalPropertySnapshot
            {
                Category = category,
                Name = name,
                Value = value
            };
        }
        private static ExternalTopologyPathNodeSnapshot Node(
            int depth,
            string instanceGuid,
            string displayName,
            bool hasChildren)
        {
            return new ExternalTopologyPathNodeSnapshot
            {
                DepthFromItem = depth,
                InstanceGuid = instanceGuid,
                DisplayName = displayName,
                ItemGuid = "item-" + instanceGuid,
                SourceFile = "source.nwc",
                HasChildren = hasChildren
            };
        }
    }
}
