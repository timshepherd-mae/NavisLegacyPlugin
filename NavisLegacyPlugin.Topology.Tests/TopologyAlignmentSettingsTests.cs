using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NavisLegacyPlugin.Services.Topology;
namespace NavisLegacyPlugin.Topology.Tests
{
 [TestClass] public class TopologyAlignmentSettingsTests
 {
  [TestMethod] public void Defaults_GoodThreshold_80(){Assert.AreEqual(80,new TopologyAlignmentSettings().GoodThreshold);}
  [TestMethod] public void Defaults_VagueThreshold_50(){Assert.AreEqual(50,new TopologyAlignmentSettings().VagueThreshold);}
  [TestMethod] public void Defaults_Separation_15(){Assert.AreEqual(15,new TopologyAlignmentSettings().MinimumSeparation);}
  [TestMethod] public void Defaults_AnchorWeight_40(){Assert.AreEqual(40,new TopologyAlignmentSettings().AnchorWeight);}
  [TestMethod] public void Defaults_OwnerWeight_32(){Assert.AreEqual(32,new TopologyAlignmentSettings().OwnerWeight);}
  [TestMethod] public void Defaults_ContextMinimum_8(){Assert.AreEqual(8,new TopologyAlignmentSettings().MinimumContextWeight);}
  [TestMethod] public void Validate_Defaults_Pass(){new TopologyAlignmentSettings().Validate();}
  [TestMethod] public void Validate_GoodBelowZero_Throws(){var s=new TopologyAlignmentSettings{GoodThreshold=-1};Assert.ThrowsException<InvalidOperationException>(()=>s.Validate());}
  [TestMethod] public void Validate_GoodAbove100_Throws(){var s=new TopologyAlignmentSettings{GoodThreshold=101};Assert.ThrowsException<InvalidOperationException>(()=>s.Validate());}
  [TestMethod] public void Validate_VagueAboveGood_Throws(){var s=new TopologyAlignmentSettings{GoodThreshold=70,VagueThreshold=71};Assert.ThrowsException<InvalidOperationException>(()=>s.Validate());}
  [TestMethod] public void Validate_SeparationAbove100_Throws(){var s=new TopologyAlignmentSettings{MinimumSeparation=101};Assert.ThrowsException<InvalidOperationException>(()=>s.Validate());}
  [TestMethod] public void Validate_ZeroAnchorWeight_Throws(){var s=new TopologyAlignmentSettings{AnchorWeight=0};Assert.ThrowsException<InvalidOperationException>(()=>s.Validate());}
 }
}