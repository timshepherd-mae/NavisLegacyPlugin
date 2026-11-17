using System.Collections.Generic;
using System.Runtime.Serialization;

namespace NavisLegacyPlugin.Models.ExternalSources
{
    [DataContract]
    public sealed class ExternalTopologyPathNodeSnapshot
    {
        [DataMember(Order = 1)]
        public int DepthFromItem { get; set; }

        [DataMember(Order = 2)]
        public string InstanceGuid { get; set; }

        [DataMember(Order = 3)]
        public string DisplayName { get; set; }

        [DataMember(Order = 4)]
        public string ItemGuid { get; set; }

        [DataMember(Order = 5)]
        public string SourceFile { get; set; }

        [DataMember(Order = 6)]
        public bool HasChildren { get; set; }
    }

    [DataContract]
    public sealed class ExternalModelItemSnapshot
    {
        public ExternalModelItemSnapshot()
        {
            Properties = new List<ExternalPropertySnapshot>();
            AncestorPath = new List<ExternalTopologyPathNodeSnapshot>();
            TopologyPath = new List<ExternalTopologyPathNodeSnapshot>();
            RidOwnerDepthFromAnchor = -1;
        }

        [DataMember(Order = 1)]
        public string InstanceGuid { get; set; }

        [DataMember(Order = 2)]
        public string DisplayName { get; set; }

        [DataMember(Order = 3)]
        public bool HasChildren { get; set; }

        [DataMember(Order = 4)]
        public List<ExternalPropertySnapshot> Properties { get; set; }

        /// <summary>
        /// Complete path from the extracted item at depth zero towards model-top.
        /// Retained for compatibility and diagnostics.
        /// </summary>
        [DataMember(Order = 5)]
        public List<ExternalTopologyPathNodeSnapshot> AncestorPath { get; set; }

        /// <summary>
        /// True when the worker identified the SOURCE item that owns the
        /// transferable RID represented by this anchor snapshot.
        /// </summary>
        [DataMember(Order = 6)]
        public bool HasRidOwner { get; set; }

        /// <summary>
        /// Exact number of Parent traversals from this anchor to the SOURCE
        /// RID owner. Zero means that the extracted item is itself the owner.
        /// Minus one means that no owner was resolved.
        /// </summary>
        [DataMember(Order = 7)]
        public int RidOwnerDepthFromAnchor { get; set; }

        /// <summary>
        /// Serializable fingerprint of the actual SOURCE RID owner.
        /// </summary>
        [DataMember(Order = 8)]
        public ExternalTopologyPathNodeSnapshot RidOwner { get; set; }

        /// <summary>
        /// Ordered anchor-to-owner path with limited context above the owner.
        /// Depth zero is always the extracted anchor item.
        /// </summary>
        [DataMember(Order = 9)]
        public List<ExternalTopologyPathNodeSnapshot> TopologyPath { get; set; }
    }

    [DataContract]
    public sealed class ExternalPropertySnapshot
    {
        [DataMember(Order = 1)]
        public string Category { get; set; }

        [DataMember(Order = 2)]
        public string Name { get; set; }

        [DataMember(Order = 3)]
        public string Value { get; set; }
    }
}
