using System;
using System.Collections.Generic;
using System.Data;
using Autodesk.Navisworks.Api;

namespace NavisLegacyPlugin.Services.Matching
{
    public enum MatchResolutionStatus
    {
        Matched,
        Unmatched,
        Ambiguous,
        MissingKey
    }

    public sealed class MatchStrategyDefinition
    {
        public MatchStrategyDefinition(
            string name,
            int priority,
            bool enabled,
            Func<DataRow, IReadOnlyList<string>> sourceKeySelector,
            Func<ModelItem, IReadOnlyList<string>> targetKeySelector)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Strategy name is required.", "name");
            Name = name;
            Priority = priority;
            Enabled = enabled;
            SourceKeySelector = sourceKeySelector ?? throw new ArgumentNullException("sourceKeySelector");
            TargetKeySelector = targetKeySelector ?? throw new ArgumentNullException("targetKeySelector");
        }

        public string Name { get; private set; }
        public int Priority { get; private set; }
        public bool Enabled { get; private set; }
        public Func<DataRow, IReadOnlyList<string>> SourceKeySelector { get; private set; }
        public Func<ModelItem, IReadOnlyList<string>> TargetKeySelector { get; private set; }
    }

    public sealed class MatchResolution
    {
        public MatchResolution(MatchResolutionStatus status, ModelItem item, string strategyName, string key)
        {
            Status = status; Item = item; StrategyName = strategyName; Key = key;
        }
        public MatchResolutionStatus Status { get; private set; }
        public ModelItem Item { get; private set; }
        public string StrategyName { get; private set; }
        public string Key { get; private set; }
    }

    public sealed class OrderedMatchDiagnostics
    {
        private readonly Dictionary<string, int> _matchedByStrategy = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public IReadOnlyDictionary<string, int> MatchedByStrategy { get { return _matchedByStrategy; } }
        public int Unmatched { get; internal set; }
        public int Ambiguous { get; internal set; }
        public int MissingKey { get; internal set; }
        internal void RecordMatch(string strategy)
        {
            int count; _matchedByStrategy.TryGetValue(strategy, out count); _matchedByStrategy[strategy] = count + 1;
        }
    }

    public interface IRowMatchResolver
    {
        MatchResolution Resolve(DataRow row);
        OrderedMatchDiagnostics Diagnostics { get; }
    }
}
