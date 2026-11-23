using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NavisLegacyPlugin.Services.Topology;
namespace NavisLegacyPlugin.Topology.Tests
{
 [TestClass] public class StructuralBranchEvidenceAnalyzerTests
 {
  private static StructuralBranchEvidenceRow R(int row,string rid,int depth,string key){return new StructuralBranchEvidenceRow(row,rid,depth,key,key,string.Empty);}
  [TestMethod] public void HomogeneousBranch_SelectsAllLeaves(){var x=StructuralBranchEvidenceAnalyzer.AnalyseAndSelect(new[]{R(1,"A",1,"B"),R(2,"A",1,"B")});Assert.AreEqual(2,x.Selections.Count);Assert.IsTrue(x.Clusters[0].IsHomogeneous);}
  [TestMethod] public void MixedBranch_IsNotSelected(){var x=StructuralBranchEvidenceAnalyzer.AnalyseAndSelect(new[]{R(1,"A",1,"B"),R(2,"B",1,"B")});Assert.AreEqual(0,x.Selections.Count);Assert.AreEqual(2,x.NoCandidateRowCount);}
  [TestMethod] public void NearestHomogeneousBranch_Wins(){var x=StructuralBranchEvidenceAnalyzer.AnalyseAndSelect(new[]{R(1,"A",1,"LOW"),R(1,"A",3,"HIGH"),R(2,"A",1,"LOW"),R(2,"A",3,"HIGH")});Assert.IsTrue(x.Selections.All(v=>v.BranchKey=="LOW"));}
  [TestMethod] public void MixedLower_FallsBackToHomogeneousHigher(){var x=StructuralBranchEvidenceAnalyzer.AnalyseAndSelect(new[]{R(1,"A",1,"LOW"),R(2,"B",1,"LOW"),R(1,"A",2,"HIGH"),R(3,"A",2,"HIGH")});Assert.AreEqual("HIGH",x.Selections.Single(v=>v.SourceRow==1).BranchKey);}
  [TestMethod] public void SameDepthCandidates_AreAmbiguous(){var x=StructuralBranchEvidenceAnalyzer.AnalyseAndSelect(new[]{R(1,"A",1,"B1"),R(1,"A",1,"B2")});Assert.AreEqual(1,x.AmbiguousNearestRowCount);Assert.AreEqual(0,x.Selections.Count);}
  [TestMethod] public void SeparateRids_SelectSeparateOwners(){var x=StructuralBranchEvidenceAnalyzer.AnalyseAndSelect(new[]{R(1,"A",1,"B1"),R(2,"B",1,"B2")});Assert.AreEqual(2,x.Selections.Select(v=>v.BranchKey).Distinct().Count());}
 }
}
