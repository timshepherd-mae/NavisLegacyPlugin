using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Autodesk.Navisworks.Api;

namespace NavisLegacyPlugin.Services.Matching
{
    public sealed class OrderedRowMatchResolver : IRowMatchResolver
    {
        private sealed class StrategyIndex
        {
            public MatchStrategyDefinition Definition;
            public Dictionary<string, List<ModelItem>> Items;
        }

        private readonly List<StrategyIndex> _indexes;
        private readonly OrderedMatchDiagnostics _diagnostics = new OrderedMatchDiagnostics();

        public OrderedRowMatchResolver(IEnumerable<ModelItem> targetItems, IEnumerable<MatchStrategyDefinition> strategies)
        {
            if (targetItems == null) throw new ArgumentNullException("targetItems");
            if (strategies == null) throw new ArgumentNullException("strategies");
            List<ModelItem> targets = targetItems.Where(i => i != null).Distinct().ToList();
            _indexes = strategies.Where(s => s != null && s.Enabled).OrderBy(s => s.Priority).Select(s => BuildIndex(targets, s)).ToList();
            if (_indexes.Count == 0) throw new InvalidOperationException("At least one enabled match strategy is required.");
        }

        public OrderedMatchDiagnostics Diagnostics { get { return _diagnostics; } }

        public MatchResolution Resolve(DataRow row)
        {
            if (row == null) throw new ArgumentNullException("row");
            bool hadKey = false;
            foreach (StrategyIndex index in _indexes)
            {
                string key = MatchKey.Create(index.Definition.SourceKeySelector(row));
                if (key == null) continue;
                hadKey = true;
                List<ModelItem> matches;
                if (!index.Items.TryGetValue(key, out matches)) continue;
                if (matches.Count > 1)
                {
                    _diagnostics.Ambiguous++;
                    return new MatchResolution(MatchResolutionStatus.Ambiguous, null, index.Definition.Name, key);
                }
                _diagnostics.RecordMatch(index.Definition.Name);
                return new MatchResolution(MatchResolutionStatus.Matched, matches[0], index.Definition.Name, key);
            }
            if (!hadKey)
            {
                _diagnostics.MissingKey++;
                return new MatchResolution(MatchResolutionStatus.MissingKey, null, null, null);
            }
            _diagnostics.Unmatched++;
            return new MatchResolution(MatchResolutionStatus.Unmatched, null, null, null);
        }

        private static StrategyIndex BuildIndex(IEnumerable<ModelItem> targets, MatchStrategyDefinition definition)
        {
            var values = new Dictionary<string, List<ModelItem>>(StringComparer.Ordinal);
            foreach (ModelItem item in targets)
            {
                string key = MatchKey.Create(definition.TargetKeySelector(item));
                if (key == null) continue;
                List<ModelItem> matches;
                if (!values.TryGetValue(key, out matches)) { matches = new List<ModelItem>(); values.Add(key, matches); }
                if (!matches.Contains(item)) matches.Add(item);
            }
            return new StrategyIndex { Definition = definition, Items = values };
        }
    }

    internal static class MatchKey
    {
        public static string Create(IReadOnlyList<string> components)
        {
            if (components == null || components.Count == 0) return null;
            var normalized = new List<string>();
            foreach (string component in components)
            {
                if (string.IsNullOrWhiteSpace(component)) return null;
                normalized.Add(component.Trim().ToUpperInvariant());
            }
            return string.Join("", normalized.ToArray());
        }
    }
}
