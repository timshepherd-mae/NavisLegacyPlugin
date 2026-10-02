using System;
using System.Collections.Generic;
using NavisLegacyPlugin.Models.Execution;

namespace NavisLegacyPlugin.Services.Execution
{
    /// <summary>
    /// Single accounting authority for one execution. Match counts are based
    /// on final per-row outcomes; write counters remain operation counts.
    /// </summary>
    public sealed class TransferStatistics
    {
        private readonly List<MatchOutcome> _outcomes = new List<MatchOutcome>();

        public IReadOnlyList<MatchOutcome> Outcomes
        {
            get { return _outcomes.AsReadOnly(); }
        }

        public int Matched { get; private set; }
        public int Unmatched { get; private set; }
        public int Written { get; private set; }
        public int Skipped { get; private set; }
        public int Failed { get; private set; }

        public void Record(MatchOutcome outcome)
        {
            if (outcome == null)
                throw new ArgumentNullException("outcome");

            _outcomes.Add(outcome);

            if (outcome.Status == MatchOutcomeStatus.Matched)
                Matched++;
            else
                Unmatched++;
        }

        public void RecordWritten()
        {
            Written++;
        }

        public void RecordSkipped()
        {
            Skipped++;
        }

        public void RecordFailed()
        {
            Failed++;
        }
    }
}
