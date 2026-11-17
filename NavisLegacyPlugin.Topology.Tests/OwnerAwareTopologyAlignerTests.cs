using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NavisLegacyPlugin.Models.Topology;
using NavisLegacyPlugin.Services.Topology;
namespace NavisLegacyPlugin.Topology.Tests
{
    [TestClass]
    public class OwnerAwareTopologyAlignerTests
    {
        [TestMethod] public void OwnerDepthTwo_AppliesOwnerWeightAtIndexTwo(){
            var s=new TopologyAlignmentSettings();
            var r=new OrderedTopologyAligner(s).Align(
                TestData.F("c"),
                TestData.Path("Anchor","Route","Owner"),
                TestData.Path("Anchor","Route","Owner"),
                2
            );
            Assert.AreEqual(s.OwnerWeight,r.Alignment.Single(x=>x.SourceIndex==2).Weight);
        }

        [TestMethod] public void IntermediateNode_RemainsStrongRouteEvidence(){var s=new TopologyAlignmentSettings();var r=new OrderedTopologyAligner(s).Align(TestData.F("c"),TestData.Path("Anchor","Route","Owner"),TestData.Path("Anchor","Route","Owner"),2);int w=r.Alignment.Single(x=>x.SourceIndex==1).Weight;Assert.IsTrue(w>=s.OwnerWeight&&w<=s.AnchorWeight);}
        [TestMethod] public void ContextAboveOwner_ReducesProgressively(){var s=new TopologyAlignmentSettings();var r=new OrderedTopologyAligner(s).Align(TestData.F("c"),TestData.Path("Anchor","Owner","C1","C2"),TestData.Path("Anchor","Owner","C1","C2"),1);Assert.IsTrue(r.Alignment.Single(x=>x.SourceIndex==3).Weight<=r.Alignment.Single(x=>x.SourceIndex==2).Weight);}
        [TestMethod] public void CompatibilityOverload_UsesIndexOneOwner(){var s=new TopologyAlignmentSettings();var r=new OrderedTopologyAligner(s).Align(TestData.F("c"),TestData.Path("Anchor","Owner"),TestData.Path("Anchor","Owner"));Assert.AreEqual(s.OwnerWeight,r.Alignment.Single(x=>x.SourceIndex==1).Weight);}
    }
}
