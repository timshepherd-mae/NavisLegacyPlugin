using System.Collections.Generic;
using System.Runtime.Serialization;

namespace NavisLegacyPlugin.Models.ExternalSources
{
    [DataContract]
    public enum ExternalRidOwnerEvidenceStatus
    {
        [EnumMember] None,
        [EnumMember] LeafOwned,
        [EnumMember] BranchOwned,
        [EnumMember] Inconsistent,
        [EnumMember] Ambiguous
    }

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
            RidOwnerEvidenceStatus = ExternalRidOwnerEvidenceStatus.None;
            RidOwnerEvidenceReason = string.Empty;
            AnchorRid = string.Empty;
            RidOwnerRid = string.Empty;
            RidPropagationBoundaryDepth = -1;
            RecoveredRidOwnerDepthFromAnchor = -1;
            RecoveredRidOwnerReason = string.Empty;
            RecoveredTopologyPath = new List<ExternalTopologyPathNodeSnapshot>();
            RecoveredTransparentNodeCount = 0;
            RecoveredSameRidBranchCount = 0;
            RecoveredBoundaryRid = string.Empty;
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

        /// <summary>
        /// T03-A diagnostic classification of the owner evidence produced by
        /// the existing recovery algorithm. This is evidence only and does not
        /// change owner selection policy.
        /// </summary>
        [DataMember(Order = 10)]
        public ExternalRidOwnerEvidenceStatus RidOwnerEvidenceStatus { get; set; }

        [DataMember(Order = 11)]
        public string RidOwnerEvidenceReason { get; set; }

        [DataMember(Order = 12)]
        public string AnchorRid { get; set; }

        [DataMember(Order = 13)]
        public string RidOwnerRid { get; set; }

        /// <summary>
        /// First ancestor depth where the observed RID no longer equals the
        /// anchor RID. Minus one means that no boundary was observed.
        /// </summary>
        [DataMember(Order = 14)]
        public int RidPropagationBoundaryDepth { get; set; }

        /// <summary>
        /// T04-A deterministic contiguous-segment owner evidence. These fields
        /// are diagnostic in T04-A. T05 will explicitly decide when the
        /// coordinator consumes them for applicability and mutation planning.
        /// </summary>
        [DataMember(Order = 15)]
        public int RecoveredRidOwnerDepthFromAnchor { get; set; }

        [DataMember(Order = 16)]
        public ExternalTopologyPathNodeSnapshot RecoveredRidOwner { get; set; }

        [DataMember(Order = 17)]
        public string RecoveredRidOwnerReason { get; set; }

        [DataMember(Order = 18)]
        public List<ExternalTopologyPathNodeSnapshot> RecoveredTopologyPath { get; set; }

        [DataMember(Order = 19)]
        public int RecoveredTransparentNodeCount { get; set; }

        [DataMember(Order = 20)]
        public int RecoveredSameRidBranchCount { get; set; }

        [DataMember(Order = 21)]
        public string RecoveredBoundaryRid { get; set; }
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
