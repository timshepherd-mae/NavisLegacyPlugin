using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NavisLegacyPlugin.Models.Topology;
using NavisLegacyPlugin.Services.Topology;
namespace NavisLegacyPlugin.Topology.Tests
{
 [TestClass] public class OrderedTopologyAlignerTests
 {
  private OrderedTopologyAligner A(){return new OrderedTopologyAligner(new TopologyAlignmentSettings());}
  [TestMethod] public void ExactPath_Score100(){var r=A().Align(TestData.F("c"),TestData.Path("A","B","C"),TestData.Path("A","B","C"));Assert.AreEqual(100,r.Score);}
  [TestMethod] public void ExactPath_AllExact(){var r=A().Align(TestData.F("c"),TestData.Path("A","B"),TestData.Path("A","B"));Assert.IsTrue(r.Alignment.All(x=>x.Disposition==TopologyAlignmentDisposition.Exact));}
  [TestMethod] public void TrimmedNames_AreExact(){var r=A().Align(TestData.F("c"),TestData.Path(" A "),TestData.Path("a"));Assert.AreEqual(100,r.Score);}
  [TestMethod] public void CaseInsensitiveNames_AreExact(){var r=A().Align(TestData.F("c"),TestData.Path("ROOF"),TestData.Path("roof"));Assert.AreEqual(100,r.Score);}
  [TestMethod] public void Rename_ContainsSubstitution(){var r=A().Align(TestData.F("c"),TestData.Path("A","Carrots","E"),TestData.Path("A","Bananas","E"));Assert.IsTrue(r.Alignment.Any(x=>x.Disposition==TopologyAlignmentDisposition.Substituted));}
  [TestMethod] public void Insertion_IsRecorded(){var r=A().Align(TestData.F("c"),TestData.Path("A","B"),TestData.Path("A","X","B"));Assert.IsTrue(r.Alignment.Any(x=>x.Disposition==TopologyAlignmentDisposition.Inserted));}
  [TestMethod] public void Deletion_IsRecorded(){var r=A().Align(TestData.F("c"),TestData.Path("A","X","B"),TestData.Path("A","B"));Assert.IsTrue(r.Alignment.Any(x=>x.Disposition==TopologyAlignmentDisposition.Deleted));}
  [TestMethod] public void EmptyPaths_ScoreZero(){var r=A().Align(TestData.F("c"),TestData.Path(),TestData.Path());Assert.AreEqual(0,r.Score);}
  [TestMethod] public void NullPaths_DoNotThrow(){var r=A().Align(TestData.F("c"),null,null);Assert.AreEqual(0,r.Score);}
  [TestMethod] public void Candidate_IsPreserved(){var f=TestData.F("candidate");var r=A().Align(f,TestData.Path("A"),TestData.Path("A"));Assert.AreSame(f,r.Candidate);}
  [TestMethod] public void TargetPath_IsPreserved(){var p=TestData.Path("A","B");var r=A().Align(TestData.F("c"),p,p);Assert.AreEqual(2,r.TargetPath.Count);}
  [TestMethod] public void Score_IsClamped(){var r=A().Align(TestData.F("c"),TestData.Path("A"),TestData.Path("B"));Assert.IsTrue(r.Score>=0&&r.Score<=100);}
  [TestMethod] public void RepeatedNames_PreserveOrder(){var r=A().Align(TestData.F("c"),TestData.Path("A","C","C","E"),TestData.Path("A","X","C","C","E"));Assert.AreEqual("E",r.Alignment.Last().TargetName);}
  [TestMethod] public void ExactSingleNode_HasPositiveWeight(){var r=A().Align(TestData.F("c"),TestData.Path("A"),TestData.Path("A"));Assert.IsTrue(r.Alignment[0].Weight>0);}
 }
}