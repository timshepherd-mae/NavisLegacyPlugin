using System;

namespace NavisLegacyPlugin.Services.Topology
{
    public enum TopologyApplicabilityReasonCode
    {
        ApplicableBranchOwner,
        SourceIsBranch,
        RidMissing,
        AnchorKeyMissing,
        OwnerNotResolved,
        OwnerDepthZero,
        OwnerIsLeaf,
        OwnerEvidenceInconsistent,
        AnchorUnmatched,
        AnchorAmbiguous
    }

    public sealed class TopologyApplicabilityAssessment
    {
        public TopologyApplicabilityAssessment(
            bool isApplicable,
            TopologyApplicabilityReasonCode reasonCode,
            string reason)
        {
            IsApplicable = isApplicable;
            ReasonCode = reasonCode;
            Reason = reason ?? string.Empty;
        }

        public bool IsApplicable { get; private set; }
        public TopologyApplicabilityReasonCode ReasonCode { get; private set; }
        public string Reason { get; private set; }
    }

    public static class TopologyApplicabilityRules
    {
        public static bool IsBranchOwnerRoute(
            bool sourceHasChildren,
            bool hasRidOwner,
            int ownerDepthFromAnchor,
            bool ownerHasChildren,
            string ridValue,
            string anchorKey)
        {
            return Assess(
                sourceHasChildren,
                hasRidOwner,
                ownerDepthFromAnchor,
                ownerHasChildren,
                ridValue,
                anchorKey).IsApplicable;
        }

        public static TopologyApplicabilityAssessment Assess(
            bool sourceHasChildren,
            bool hasRidOwner,
            int ownerDepthFromAnchor,
            bool ownerHasChildren,
            string ridValue,
            string anchorKey)
        {
            if (sourceHasChildren)
            {
                return Result(false, TopologyApplicabilityReasonCode.SourceIsBranch,
                    "SOURCE branch rows are route context and are not definitive topology leaf anchors.");
            }

            if (!IsTransferableRid(ridValue))
            {
                return Result(false, TopologyApplicabilityReasonCode.RidMissing,
                    "The SOURCE row has no transferable RID.");
            }

            if (string.IsNullOrWhiteSpace(anchorKey))
            {
                return Result(false, TopologyApplicabilityReasonCode.AnchorKeyMissing,
                    "The selected Anchor Method did not produce a complete SOURCE key.");
            }

            if (!hasRidOwner)
            {
                return Result(false, TopologyApplicabilityReasonCode.OwnerNotResolved,
                    "The external worker did not retain a SOURCE RID owner.");
            }

            if (ownerDepthFromAnchor < 0)
            {
                return Result(false, TopologyApplicabilityReasonCode.OwnerEvidenceInconsistent,
                    "The retained SOURCE RID owner has an invalid negative depth.");
            }

            if (ownerDepthFromAnchor == 0)
            {
                return Result(false, TopologyApplicabilityReasonCode.OwnerDepthZero,
                    "The RID owner is the anchor itself; leaf-owned RID transfer is handled by Direct mode.");
            }

            if (!ownerHasChildren)
            {
                return Result(false, TopologyApplicabilityReasonCode.OwnerIsLeaf,
                    "The retained SOURCE RID owner is not a branch.");
            }

            return Result(true, TopologyApplicabilityReasonCode.ApplicableBranchOwner,
                "The SOURCE leaf has a complete branch-owner topology relationship.");
        }

        private static bool IsTransferableRid(string value)
        {
            return !string.IsNullOrWhiteSpace(value)
                && !string.Equals(value.Trim(), "\"\"", StringComparison.Ordinal);
        }

        private static TopologyApplicabilityAssessment Result(
            bool applicable,
            TopologyApplicabilityReasonCode code,
            string reason)
        {
            return new TopologyApplicabilityAssessment(applicable, code, reason);
        }
    }
}
