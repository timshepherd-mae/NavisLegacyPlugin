using Microsoft.VisualStudio.TestTools.UnitTesting;
using NavisLegacyPlugin.Models.Topology;
using NavisLegacyPlugin.Services.Topology;
namespace NavisLegacyPlugin.Topology.Tests
{
 [TestClass] public class TopologyConfidenceClassifierTests
 {
  private TopologyConfidenceClassifier C(){return new TopologyConfidenceClassifier(new TopologyAlignmentSettings());}
  private TopologyResolution R(int best,int next=0){return C().Classify(new[]{TestData.Anchor()},TestData.F("owner"),"RID",TestData.Path("A"),next>0?new[]{TestData.Candidate(best,"a"),TestData.Candidate(next,"b")}:new[]{TestData.Candidate(best)});}
  [TestMethod] public void Score80_IsGood(){Assert.AreEqual(TopologyResolutionStatus.Good,R(80).Status);}
  [TestMethod] public void Score100_IsGood(){Assert.IsTrue(R(100).WriteEligible);}
  [TestMethod] public void Score79_IsVague(){Assert.AreEqual(TopologyResolutionStatus.Vague,R(79).Status);}
  [TestMethod] public void Score50_IsVague(){Assert.AreEqual(TopologyResolutionStatus.Vague,R(50).Status);}
  [TestMethod] public void Score49_IsUnresolved(){Assert.AreEqual(TopologyResolutionStatus.Unresolved,R(49).Status);}
  [TestMethod] public void Separation14_IsAmbiguous(){Assert.AreEqual(TopologyResolutionStatus.Ambiguous,R(90,76).Status);}
  [TestMethod] public void Separation15_AllowsGood(){Assert.AreEqual(TopologyResolutionStatus.Good,R(90,75).Status);}
  [TestMethod] public void MissingAnchor_IsUnresolved(){var r=C().Classify(new[]{TestData.Anchor(TopologyAnchorStatus.MissingKey)},TestData.F("o"),"R",TestData.Path("A"),new[]{TestData.Candidate(100)});Assert.AreEqual(TopologyResolutionStatus.Unresolved,r.Status);}
  [TestMethod] public void NoAnchors_IsUnresolved(){var r=C().Classify(null,TestData.F("o"),"R",TestData.Path("A"),new[]{TestData.Candidate(100)});Assert.AreEqual(TopologyResolutionStatus.Unresolved,r.Status);}
  [TestMethod] public void NoCandidates_IsUnresolved(){var r=C().Classify(new[]{TestData.Anchor()},TestData.F("o"),"R",TestData.Path("A"),null);Assert.AreEqual(TopologyResolutionStatus.Unresolved,r.Status);}
  [TestMethod] public void Good_StoresBestScore(){Assert.AreEqual(90,R(90).BestScore);}
  [TestMethod] public void Separation_IsDerived(){Assert.AreEqual(15,R(90,75).Separation);}
  [TestMethod] public void Vague_NotWriteEligible(){Assert.IsFalse(R(70).WriteEligible);}
  [TestMethod] public void Ambiguous_NotWriteEligible(){Assert.IsFalse(R(90,80).WriteEligible);}
 }
}