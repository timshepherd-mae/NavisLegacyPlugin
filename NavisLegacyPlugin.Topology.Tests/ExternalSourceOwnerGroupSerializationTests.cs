using System.IO;
using System.Runtime.Serialization.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NavisLegacyPlugin.Models.ExternalSources;

namespace NavisLegacyPlugin.Topology.Tests
{
    [TestClass]
    public class ExternalSourceOwnerGroupSerializationTests
    {
        [TestMethod]
        public void OwnerGroup_RoundTrips()
        {
            var value = new ExternalSourceRidOwnerGroup
            {
                RidValue = "RID-1",
                Owner = new ExternalSourceOwnerSnapshot
                {
                    InstanceGuid = "owner-i",
                    DisplayName = "Owner",
                    ItemGuid = "owner-g",
                    SourceFile = "source.nwd",
                    RidValue = "RID-1"
                }
            };
            value.Anchors.Add(new ExternalSourceAnchorSnapshot
            {
                InstanceGuid = "leaf-i",
                DisplayName = "Leaf",
                OwnerDepthFromAnchor = 2
            });
            value.Boundaries.Add(new ExternalSourceOwnershipBoundary
            {
                DepthFromOwner = 1,
                InstanceGuid = "nested-i",
                DisplayName = "Nested",
                RidValue = "RID-2",
                SameRidAsOuterOwner = false
            });
            ExternalSourceRidOwnerGroup roundTrip = RoundTrip(value);
            Assert.AreEqual("RID-1", roundTrip.RidValue);
            Assert.AreEqual("Owner", roundTrip.Owner.DisplayName);
            Assert.AreEqual(1, roundTrip.Anchors.Count);
            Assert.AreEqual(2, roundTrip.Anchors[0].OwnerDepthFromAnchor);
            Assert.AreEqual(1, roundTrip.Boundaries.Count);
        }

        private static T RoundTrip<T>(T value)
        {
            var serializer = new DataContractJsonSerializer(typeof(T));
            using (var stream = new MemoryStream())
            {
                serializer.WriteObject(stream, value);
                stream.Position = 0;
                return (T)serializer.ReadObject(stream);
            }
        }
    }
}
