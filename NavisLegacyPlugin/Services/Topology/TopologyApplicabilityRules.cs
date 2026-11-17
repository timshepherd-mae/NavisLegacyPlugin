using System;

namespace NavisLegacyPlugin.Services.Topology
{
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
            return !sourceHasChildren
                && hasRidOwner
                && ownerDepthFromAnchor > 0
                && ownerHasChildren
                && !string.IsNullOrWhiteSpace(ridValue)
                && !string.Equals(ridValue.Trim(), "\"\"", StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(anchorKey);
        }
    }
}
