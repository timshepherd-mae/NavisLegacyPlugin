using System;
using System.Collections.Generic;
using System.Linq;
using NavisLegacyPlugin.Models.Topology;

namespace NavisLegacyPlugin.Services.Topology
{
    public sealed class OrderedTopologyAligner
    {
        private readonly TopologyAlignmentSettings _settings;

        public OrderedTopologyAligner(TopologyAlignmentSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException("settings");
            _settings.Validate();
        }

        public TopologyCandidateEvidence Align(TopologyItemFingerprint candidate,
            IReadOnlyList<TopologyPathNode> sourcePath, IReadOnlyList<TopologyPathNode> targetPath)
        {
            sourcePath = sourcePath ?? new TopologyPathNode[0];
            targetPath = targetPath ?? new TopologyPathNode[0];
            int n = sourcePath.Count;
            int m = targetPath.Count;
            int[,] cost = new int[n + 1, m + 1];
            Move[,] move = new Move[n + 1, m + 1];

            for (int i = 1; i <= n; i++) { cost[i, 0] = cost[i - 1, 0] + GapPenalty(i - 1); move[i, 0] = Move.Delete; }
            for (int j = 1; j <= m; j++) { cost[0, j] = cost[0, j - 1] + GapPenalty(j - 1); move[0, j] = Move.Insert; }

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    bool exact = NamesEqual(sourcePath[i - 1].DisplayName, targetPath[j - 1].DisplayName);
                    int diagonal = cost[i - 1, j - 1] + (exact ? 0 : SubstitutionPenalty(i - 1));
                    int deletion = cost[i - 1, j] + GapPenalty(i - 1);
                    int insertion = cost[i, j - 1] + GapPenalty(j - 1);
                    if (diagonal <= deletion && diagonal <= insertion) { cost[i, j] = diagonal; move[i, j] = Move.Diagonal; }
                    else if (deletion <= insertion) { cost[i, j] = deletion; move[i, j] = Move.Delete; }
                    else { cost[i, j] = insertion; move[i, j] = Move.Insert; }
                }
            }

            var steps = new List<TopologyAlignmentStep>();
            int x = n, y = m;
            while (x > 0 || y > 0)
            {
                Move selected = move[x, y];
                if (selected == Move.Diagonal)
                {
                    int si = x - 1, ti = y - 1;
                    bool exact = NamesEqual(sourcePath[si].DisplayName, targetPath[ti].DisplayName);
                    int weight = WeightFor(si);
                    steps.Add(new TopologyAlignmentStep(si, ti, sourcePath[si].DisplayName, targetPath[ti].DisplayName,
                        exact ? TopologyAlignmentDisposition.Exact : TopologyAlignmentDisposition.Substituted,
                        exact ? weight : weight * _settings.SubstitutionScore / 100, weight));
                    x--; y--;
                }
                else if (selected == Move.Delete)
                {
                    int si = x - 1; int weight = WeightFor(si);
                    steps.Add(new TopologyAlignmentStep(si, -1, sourcePath[si].DisplayName, string.Empty,
                        TopologyAlignmentDisposition.Deleted, weight * _settings.DeletionScore / 100, weight));
                    x--;
                }
                else
                {
                    int ti = y - 1; int weight = WeightFor(ti);
                    steps.Add(new TopologyAlignmentStep(-1, ti, string.Empty, targetPath[ti].DisplayName,
                        TopologyAlignmentDisposition.Inserted, weight * _settings.InsertionScore / 100, weight));
                    y--;
                }
            }
            steps.Reverse();
            int totalWeight = steps.Sum(s => s.Weight);
            int score = totalWeight == 0 ? 0 : Math.Max(0, Math.Min(100, steps.Sum(s => s.ScoreContribution) * 100 / totalWeight));
            return new TopologyCandidateEvidence(candidate, score, targetPath, steps);
        }

        private bool NamesEqual(string a, string b) { return string.Equals(Normalize(a), Normalize(b), StringComparison.OrdinalIgnoreCase); }
        private static string Normalize(string value) { return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim(); }
        private int WeightFor(int index) { if (index == 0) return _settings.AnchorWeight; if (index == 1) return _settings.OwnerWeight; return Math.Max(_settings.MinimumContextWeight, _settings.OwnerWeight - ((index - 1) * _settings.ContextWeightReduction)); }
        private int GapPenalty(int index) { return Math.Max(1, WeightFor(index) * (100 - _settings.InsertionScore) / 100); }
        private int SubstitutionPenalty(int index) { return Math.Max(1, WeightFor(index) * (100 - _settings.SubstitutionScore) / 100); }
        private enum Move { None, Diagonal, Insert, Delete }
    }
}
