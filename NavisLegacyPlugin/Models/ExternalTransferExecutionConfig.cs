using NavisLegacyPlugin.Models.Collections;
using NavisLegacyPlugin.Models.Matching;
namespace NavisLegacyPlugin.Models
{
    public sealed class ExternalTransferExecutionConfig
    {
        public ExternalTransferExecutionConfig(string sourceFilePath,string exportSetName,
            CollectionResolutionType sourceResolution,CollectionResolutionType targetResolution,
            int sourceItemCount,int targetItemCount,bool overwrite,
            MatchingStrategyMode matchingStrategy,AnchorMethod anchorMethod)
        {
            SourceFilePath=sourceFilePath??string.Empty;ExportSetName=exportSetName??string.Empty;
            SourceResolution=sourceResolution;TargetResolution=targetResolution;
            SourceItemCount=sourceItemCount;TargetItemCount=targetItemCount;Overwrite=overwrite;
            MatchingStrategy=matchingStrategy;AnchorMethod=anchorMethod;
        }
        public string SourceFilePath{get;private set;} public string ExportSetName{get;private set;}
        public CollectionResolutionType SourceResolution{get;private set;} public CollectionResolutionType TargetResolution{get;private set;}
        public int SourceItemCount{get;private set;} public int TargetItemCount{get;private set;}
        public bool Overwrite{get;private set;} public MatchingStrategyMode MatchingStrategy{get;private set;} public AnchorMethod AnchorMethod{get;private set;}
    }
}
