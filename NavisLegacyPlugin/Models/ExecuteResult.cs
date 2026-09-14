namespace NavisLegacyPlugin.Models
{
    public sealed class ExecuteResult
    {
        public ExecuteResult(
            int matched,
            int unmatched,
            int written,
            int skipped)
        {
            Matched = matched;
            Unmatched = unmatched;
            Written = written;
            Skipped = skipped;
        }

        public int Matched { get; }

        public int Unmatched { get; }

        public int Written { get; }

        public int Skipped { get; }
    }
}
