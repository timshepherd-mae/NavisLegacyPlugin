namespace NavisLegacyPlugin.Models
{
    public sealed class ExecutionSelectionSets
    {
        public ExecutionSelectionSets(
            string[] sourcePath,
            string[] targetPath,
            string[] exportPath)
        {
            SourcePath = sourcePath;
            TargetPath = targetPath;
            ExportPath = exportPath;
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
    }
}
