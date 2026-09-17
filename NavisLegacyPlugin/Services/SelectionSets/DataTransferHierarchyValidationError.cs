namespace NavisLegacyPlugin.Services.SelectionSets
{
    public sealed class DataTransferHierarchyValidationError
    {
        public DataTransferHierarchyValidationError(
            string code,
            string message,
            string path)
        {
            Code = code;
            Message = message;
            Path = path;
        }

        public string Code { get; private set; }

        public string Message { get; private set; }

        public string Path { get; private set; }

        public override string ToString()
        {
            if (string.IsNullOrWhiteSpace(Path))
            {
                return string.Format(
                    "{0}: {1}",
                    Code,
                    Message);
            }

            return string.Format(
                "{0}: {1} ({2})",
                Code,
                Message,
                Path);
        }
    }
}
