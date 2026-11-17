using System;
using System.Collections.Generic;
using System.Linq;

namespace NavisLegacyPlugin.Services.Topology
{
    /// <summary>
    /// Host-independent deterministic representative selection policy used by
    /// tests and as the documented Drop 3 saturation contract.
    /// </summary>
    public static class RepresentativeAnchorSelector
    {
        public static IReadOnlyList<T> Select<T>(
            IEnumerable<T> values,
            int maximum,
            Func<T, string> immediateChildBranch,
            Func<T, string> displayName,
            Func<T, string> anchorKey,
            Func<T, int> stableRowIndex)
        {
            if (values == null)
            {
                return new List<T>().AsReadOnly();
            }

            if (maximum < 1)
            {
                throw new ArgumentOutOfRangeException("maximum");
            }

            if (immediateChildBranch == null
                || displayName == null
                || anchorKey == null
                || stableRowIndex == null)
            {
                throw new ArgumentNullException("selector");
            }

            List<T> ordered = values
                .OrderBy(stableRowIndex)
                .ToList();

            if (ordered.Count <= maximum)
            {
                return ordered.AsReadOnly();
            }

            var selected = new List<T>();
            var seenRows = new HashSet<int>();
            var childBranches = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var displayNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var anchorKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            AddDistinct(
                ordered,
                maximum,
                immediateChildBranch,
                stableRowIndex,
                childBranches,
                selected,
                seenRows);

            AddDistinct(
                ordered,
                maximum,
                displayName,
                stableRowIndex,
                displayNames,
                selected,
                seenRows);

            AddDistinct(
                ordered,
                maximum,
                anchorKey,
                stableRowIndex,
                anchorKeys,
                selected,
                seenRows);

            List<T> remaining = ordered
                .Where(item => !seenRows.Contains(stableRowIndex(item)))
                .ToList();

            int required = maximum - selected.Count;
            if (required > 0)
            {
                foreach (T item in EvenlyDistributed(remaining, required))
                {
                    int row = stableRowIndex(item);
                    if (seenRows.Add(row))
                    {
                        selected.Add(item);
                    }
                }
            }

            return selected
                .OrderBy(stableRowIndex)
                .Take(maximum)
                .ToList()
                .AsReadOnly();
        }

        private static void AddDistinct<T>(
            IEnumerable<T> ordered,
            int maximum,
            Func<T, string> selector,
            Func<T, int> stableRowIndex,
            ISet<string> seenValues,
            ICollection<T> selected,
            ISet<int> seenRows)
        {
            foreach (T item in ordered)
            {
                if (selected.Count >= maximum)
                {
                    return;
                }

                int row = stableRowIndex(item);
                if (seenRows.Contains(row))
                {
                    continue;
                }

                string value = Normalize(selector(item));
                if (string.IsNullOrWhiteSpace(value)
                    || !seenValues.Add(value))
                {
                    continue;
                }

                selected.Add(item);
                seenRows.Add(row);
            }
        }

        private static IEnumerable<T> EvenlyDistributed<T>(
            IReadOnlyList<T> values,
            int required)
        {
            if (values == null || values.Count == 0 || required <= 0)
            {
                yield break;
            }

            if (required >= values.Count)
            {
                foreach (T value in values)
                {
                    yield return value;
                }
                yield break;
            }

            if (required == 1)
            {
                yield return values[values.Count / 2];
                yield break;
            }

            var indexes = new HashSet<int>();
            for (int index = 0; index < required; index++)
            {
                indexes.Add(
                    (int)Math.Round(
                        index * (values.Count - 1d) / (required - 1d)));
            }

            foreach (int index in indexes.OrderBy(value => value))
            {
                yield return values[index];
            }
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty)
                .Trim()
                .ToUpperInvariant();
        }
    }
}
