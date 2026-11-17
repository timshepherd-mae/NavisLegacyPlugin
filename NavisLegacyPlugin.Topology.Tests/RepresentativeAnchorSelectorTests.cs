using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NavisLegacyPlugin.Services.Topology;

namespace NavisLegacyPlugin.Topology.Tests
{
    [TestClass]
    public class RepresentativeAnchorSelectorTests
    {
        private sealed class Row
        {
            public int Index;
            public string Child;
            public string Name;
            public string Key;
        }

        [TestMethod]
        public void TwentyFiveEquivalentAnchors_AreCappedAtFive()
        {
            List<Row> rows = Enumerable.Range(1, 25)
                .Select(index => new Row
                {
                    Index = index,
                    Child = "Branch",
                    Name = "Leaf",
                    Key = "K" + index
                })
                .ToList();

            IReadOnlyList<Row> selected = Select(rows);
            Assert.AreEqual(5, selected.Count);
        }

        [TestMethod]
        public void FewerThanFive_UsesAllAnchors()
        {
            List<Row> rows = Enumerable.Range(1, 4)
                .Select(index => new Row
                {
                    Index = index,
                    Child = "B",
                    Name = "N" + index,
                    Key = "K" + index
                })
                .ToList();

            Assert.AreEqual(4, Select(rows).Count);
        }

        [TestMethod]
        public void DistinctImmediateChildBranches_ArePreferred()
        {
            List<Row> rows = new List<Row>
            {
                new Row { Index = 1, Child = "A", Name = "Leaf", Key = "1" },
                new Row { Index = 2, Child = "A", Name = "Leaf", Key = "2" },
                new Row { Index = 3, Child = "B", Name = "Leaf", Key = "3" },
                new Row { Index = 4, Child = "C", Name = "Leaf", Key = "4" },
                new Row { Index = 5, Child = "D", Name = "Leaf", Key = "5" },
                new Row { Index = 6, Child = "E", Name = "Leaf", Key = "6" }
            };

            IReadOnlyList<Row> selected = Select(rows);
            Assert.AreEqual(5, selected.Select(row => row.Child).Distinct().Count());
        }

        [TestMethod]
        public void Selection_IsDeterministic()
        {
            List<Row> rows = Enumerable.Range(1, 15)
                .Select(index => new Row
                {
                    Index = index,
                    Child = index % 3 == 0 ? "B" : "A",
                    Name = "N" + (index % 4),
                    Key = "K" + index
                })
                .ToList();

            string first = string.Join(",", Select(rows).Select(row => row.Index));
            string second = string.Join(",", Select(rows).Select(row => row.Index));
            Assert.AreEqual(first, second);
        }

        [TestMethod]
        public void StableRowOrder_IsReturned()
        {
            List<Row> rows = Enumerable.Range(1, 12)
                .Select(index => new Row
                {
                    Index = index,
                    Child = "A",
                    Name = "Leaf",
                    Key = "Same"
                })
                .Reverse()
                .ToList();

            int[] indexes = Select(rows).Select(row => row.Index).ToArray();
            CollectionAssert.AreEqual(indexes.OrderBy(value => value).ToArray(), indexes);
        }

        private static IReadOnlyList<Row> Select(IEnumerable<Row> rows)
        {
            return RepresentativeAnchorSelector.Select(
                rows,
                5,
                row => row.Child,
                row => row.Name,
                row => row.Key,
                row => row.Index);
        }
    }
}
