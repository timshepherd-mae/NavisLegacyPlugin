using NavisLegacyPlugin.Models.Collections;
using NavisLegacyPlugin.Models.Matching;

namespace NavisLegacyPlugin.Models
{
    public sealed class ExternalTransferExecutionConfig
    {
        public ExternalTransferExecutionConfig(
            string sourceFilePath,
            string exportSetName,
            CollectionResolutionType sourceResolution,
            CollectionResolutionType targetResolution,
            int sourceItemCount,
            int targetItemCount,
            bool overwrite,
            MatchingStrategyMode matchingStrategy,
            AnchorMethod anchorMethod)
            : this(sourceFilePath, exportSetName, sourceResolution, targetResolution,
                sourceItemCount, targetItemCount, overwrite, matchingStrategy, anchorMethod,
                sourceItemCount, 0, 0, targetItemCount, 0, 0)
        {
        }

        public ExternalTransferExecutionConfig(
            string sourceFilePath,
            string exportSetName,
            CollectionResolutionType sourceResolution,
            CollectionResolutionType targetResolution,
            int sourceItemCount,
            int targetItemCount,
            bool overwrite,
            MatchingStrategyMode matchingStrategy,
            AnchorMethod anchorMethod,
            int topologySourceBoundaryCount,
            int topologySourceLeafCount,
            int topologySourceBranchOwnerCount,
            int topologyTargetBoundaryCount,
            int topologyTargetLeafCount,
            int topologyTargetBranchCount)
        {
            SourceFilePath = sourceFilePath ?? string.Empty;
            ExportSetName = exportSetName ?? string.Empty;
            SourceResolution = sourceResolution;
            TargetResolution = targetResolution;
            SourceItemCount = sourceItemCount;
            TargetItemCount = targetItemCount;
            Overwrite = overwrite;
            MatchingStrategy = matchingStrategy;
            AnchorMethod = anchorMethod;
            TopologySourceBoundaryCount = topologySourceBoundaryCount;
            TopologySourceLeafCount = topologySourceLeafCount;
            TopologySourceBranchOwnerCount = topologySourceBranchOwnerCount;
            TopologyTargetBoundaryCount = topologyTargetBoundaryCount;
            TopologyTargetLeafCount = topologyTargetLeafCount;
            TopologyTargetBranchCount = topologyTargetBranchCount;
        }

        public string SourceFilePath { get; private set; }
        public string ExportSetName { get; private set; }
        public CollectionResolutionType SourceResolution { get; private set; }
        public CollectionResolutionType TargetResolution { get; private set; }
        public int SourceItemCount { get; private set; }
        public int TargetItemCount { get; private set; }
        public bool Overwrite { get; private set; }
        public MatchingStrategyMode MatchingStrategy { get; private set; }
        public AnchorMethod AnchorMethod { get; private set; }
        public int TopologySourceBoundaryCount { get; private set; }
        public int TopologySourceLeafCount { get; private set; }
        public int TopologySourceBranchOwnerCount { get; private set; }
        public int TopologyTargetBoundaryCount { get; private set; }
        public int TopologyTargetLeafCount { get; private set; }
        public int TopologyTargetBranchCount { get; private set; }
    }
}
