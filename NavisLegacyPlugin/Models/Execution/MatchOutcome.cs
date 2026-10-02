namespace NavisLegacyPlugin.Models.Execution
{
    public enum MatchOutcomeStatus
    {
        Matched,
        Unmatched
    }

    /// <summary>
    /// Records the final matching outcome for one mapped source row.
    /// </summary>
    public sealed class MatchOutcome
    {
        public MatchOutcome(int rowIndex, MatchOutcomeStatus status, string reason)
        {
            RowIndex = rowIndex;
            Status = status;
            Reason = reason ?? string.Empty;
        }

        public int RowIndex { get; private set; }
        public MatchOutcomeStatus Status { get; private set; }
        public string Reason { get; private set; }

        public static MatchOutcome Matched(int rowIndex)
        {
            return new MatchOutcome(rowIndex, MatchOutcomeStatus.Matched, "Matched");
        }

        public static MatchOutcome Unmatched(int rowIndex, string reason)
        {
            return new MatchOutcome(rowIndex, MatchOutcomeStatus.Unmatched, reason);
        }
    }
}
