using System.IO;
using System.Runtime.Serialization.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NavisLegacyPlugin.Models.Topology;
using NavisLegacyPlugin.Services.Topology;
namespace NavisLegacyPlugin.Topology.Tests
{
 [TestClass] public class TopologySerializationTests
 {
  private T RoundTrip<T>(T value){var s=new DataContractJsonSerializer(typeof(T));using(var m=new MemoryStream()){s.WriteObject(m,value);m.Position=0;return (T)s.ReadObject(m);}}
  [TestMethod] public void Fingerprint_RoundTrips(){var x=RoundTrip(TestData.F("A"));Assert.AreEqual("A",x.DisplayName);}
  [TestMethod] public void PathNode_RoundTrips(){var x=RoundTrip(TestData.Path("A")[0]);Assert.AreEqual(0,x.DepthFromAnchor);}
  [TestMethod] public void Anchor_RoundTrips(){var x=RoundTrip(TestData.Anchor());Assert.AreEqual(TopologyAnchorStatus.Matched,x.Status);}
  [TestMethod] public void Candidate_RoundTrips(){var x=RoundTrip(TestData.Candidate(88));Assert.AreEqual(88,x.Score);}
  [TestMethod] public void Resolution_RoundTrips(){var x=new TopologyConfidenceClassifier(new NavisLegacyPlugin.Services.Topology.TopologyAlignmentSettings()).Classify(new[]{TestData.Anchor()},TestData.F("o"),"RID-1",TestData.Path("A"),new[]{TestData.Candidate(90)});var y=RoundTrip(x);Assert.AreEqual("RID-1",y.RidValue);}
 }
}