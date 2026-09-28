using System;
using System.Collections.Generic;
using System.Data;
using Autodesk.Navisworks.Api;

namespace NavisLegacyPlugin.Services.Matching
{
    public static class MatchStrategyFactory
    {
        public static MatchStrategyDefinition InstanceGuid(int priority)
        {
            return new MatchStrategyDefinition(
                "InstanceGuid",
                priority,
                true,
                row => new[] { ReadGuid(row, "InstanceGuid") },
                item => new[] { item.InstanceGuid.ToString("D") });
        }

        private static string ReadGuid(DataRow row, string columnName)
        {
            if (row == null || row.Table == null || !row.Table.Columns.Contains(columnName)) return null;
            string value = Convert.ToString(row[columnName]);
            Guid parsed;
            return Guid.TryParse(value, out parsed) ? parsed.ToString("D") : null;
        }
    }
}
