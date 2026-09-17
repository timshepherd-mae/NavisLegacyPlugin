namespace NavisLegacyPlugin.Services.SelectionSets
{
    public static class DataTransferSelectionSetNames
    {
        public const string Root = "MAE-4D";

        public const string DataTransfer = "DATA-TRANSFER";

        public const string Source = "SOURCE";

        public const string Target = "TARGET";

        public const string Export = "EXPORT";

        public static readonly string[] SourcePath =
        {
            Root,
            DataTransfer,
            Source
        };

        public static readonly string[] TargetPath =
        {
            Root,
            DataTransfer,
            Target
        };

        public static readonly string[] ExportPath =
        {
            Root,
            DataTransfer,
            Export
        };
    }
}
