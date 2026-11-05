using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NavisLegacyPlugin.Services.Topology;

namespace NavisLegacyPlugin.Topology.Tests
{
    [TestClass]
    public sealed class TopologyAlignmentSettingsTests
    {
        [TestMethod]
        public void Defaults_MatchLockedConfidencePolicy()
        {
            var settings = new TopologyAlignmentSettings();
            Assert.AreEqual(80, settings.GoodThreshold);
            Assert.AreEqual(50, settings.VagueThreshold);
            Assert.AreEqual(15, settings.MinimumSeparation);
            Assert.AreEqual(100, settings.ExactScore);
            Assert.AreEqual(35, settings.SubstitutionScore);
            Assert.AreEqual(20, settings.InsertionScore);
            Assert.AreEqual(20, settings.DeletionScore);
            Assert.AreEqual(40, settings.AnchorWeight);
            Assert.AreEqual(32, settings.OwnerWeight);
            Assert.AreEqual(8, settings.MinimumContextWeight);
            Assert.AreEqual(6, settings.ContextWeightReduction);
        }

        [TestMethod]
        public void Validate_RejectsInvalidThresholdsAndWeights()
        {
            AssertInvalid(delegate { new TopologyAlignmentSettings { GoodThreshold = 101 }.Validate(); });
            AssertInvalid(delegate { new TopologyAlignmentSettings { VagueThreshold = 81 }.Validate(); });
            AssertInvalid(delegate { new TopologyAlignmentSettings { MinimumSeparation = -1 }.Validate(); });
            AssertInvalid(delegate { new TopologyAlignmentSettings { AnchorWeight = 0 }.Validate(); });
            AssertInvalid(delegate { new TopologyAlignmentSettings { OwnerWeight = 0 }.Validate(); });
            AssertInvalid(delegate { new TopologyAlignmentSettings { MinimumContextWeight = 0 }.Validate(); });
        }

        private static void AssertInvalid(Action action)
        {
            Assert.ThrowsException<InvalidOperationException>(action);
        }
    }
}
