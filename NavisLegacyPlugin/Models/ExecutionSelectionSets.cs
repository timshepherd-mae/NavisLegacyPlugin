using NavisLegacyPlugin.Models.Collections;

namespace NavisLegacyPlugin.Models
{
    public sealed class ExecutionSelectionSets
    {
        public ExecutionSelectionSets(
            string[] sourcePath,
            string[] targetPath,
            string[] exportPath)
            : this(sourcePath, targetPath, exportPath, null, null, null)
        {
        }

        public ExecutionSelectionSets(
            string[] sourcePath,
            string[] targetPath,
            string[] exportPath,
            CollectionResolutionType? sourceResolutionType,
            CollectionResolutionType? targetResolutionType,
            CollectionResolutionType? exportResolutionType)
        {
            SourcePath = sourcePath;
            TargetPath = targetPath;
            ExportPath = exportPath;
            SourceResolutionType = sourceResolutionType;
            TargetResolutionType = targetResolutionType;
            ExportResolutionType = exportResolutionType;
        }

        public string[] SourcePath
        {
            get;
            private set;
        }

        public string[] TargetPath
        {
            get;
            private set;
        }

        public string[] ExportPath
        {
            get;
            private set;
        }

        public CollectionResolutionType? SourceResolutionType
        {
            get;
            private set;
        }

        public CollectionResolutionType? TargetResolutionType
        {
            get;
            private set;
        }

        public CollectionResolutionType? ExportResolutionType
        {
            get;
            private set;
        }
    }
}
