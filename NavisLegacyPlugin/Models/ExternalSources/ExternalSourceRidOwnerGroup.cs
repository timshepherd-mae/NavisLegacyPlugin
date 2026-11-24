using System.Collections.Generic;
using System.Runtime.Serialization;

namespace NavisLegacyPlugin.Models.ExternalSources
{
    [DataContract]
    public sealed class ExternalSourceOwnerSnapshot
    {
        [DataMember(Order = 1)] public string InstanceGuid { get; set; }
        [DataMember(Order = 2)] public string DisplayName { get; set; }
        [DataMember(Order = 3)] public string ItemGuid { get; set; }
        [DataMember(Order = 4)] public string SourceFile { get; set; }
        [DataMember(Order = 5)] public string RidValue { get; set; }
    }

    [DataContract]
    public sealed class ExternalSourceAnchorSnapshot
    {
        public ExternalSourceAnchorSnapshot()
        {
            Properties = new List<ExternalPropertySnapshot>();
            OwnerRelativePath = new List<ExternalTopologyPathNodeSnapshot>();
        }
        [DataMember(Order = 1)] public string InstanceGuid { get; set; }
        [DataMember(Order = 2)] public string DisplayName { get; set; }
        [DataMember(Order = 3)] public int OwnerDepthFromAnchor { get; set; }
        [DataMember(Order = 4)] public List<ExternalPropertySnapshot> Properties { get; set; }
        [DataMember(Order = 5)] public List<ExternalTopologyPathNodeSnapshot> OwnerRelativePath { get; set; }
    }

    [DataContract]
    public sealed class ExternalSourceOwnershipBoundary
    {
        [DataMember(Order = 1)] public int DepthFromOwner { get; set; }
        [DataMember(Order = 2)] public string InstanceGuid { get; set; }
        [DataMember(Order = 3)] public string DisplayName { get; set; }
        [DataMember(Order = 4)] public string RidValue { get; set; }
        [DataMember(Order = 5)] public bool SameRidAsOuterOwner { get; set; }
    }

    [DataContract]
    public sealed class ExternalSourceRidOwnerGroup
    {
        public ExternalSourceRidOwnerGroup()
        {
            Anchors = new List<ExternalSourceAnchorSnapshot>();
            Boundaries = new List<ExternalSourceOwnershipBoundary>();
        }
        [DataMember(Order = 1)] public ExternalSourceOwnerSnapshot Owner { get; set; }
        [DataMember(Order = 2)] public string RidValue { get; set; }
        [DataMember(Order = 3)] public List<ExternalSourceAnchorSnapshot> Anchors { get; set; }
        [DataMember(Order = 4)] public List<ExternalSourceOwnershipBoundary> Boundaries { get; set; }
    }
}
