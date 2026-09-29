using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Diagnostics;
using Autodesk.Navisworks.Api;

namespace NavisLegacyPlugin.Services.Matching
{
    public static class MatchStrategyFactory
    {
        public static MatchStrategyDefinition ItemGuid(int priority)
        {
            return new MatchStrategyDefinition(
                "Item.GUID",
                priority,
                true,
                row => new[] { ReadSourceProperty(row, "Item", "GUID") },
                item => new[] { ReadTargetProperty(item, "Item", "GUID") });
        }

        public static MatchStrategyDefinition ItemGuidAndSourceFile(int priority)
        {
            return new MatchStrategyDefinition(
                "Item.GUID + Item.SourceFile",
                priority,
                true,
                row => new[]
                {
                    ReadSourceProperty(row, "Item", "GUID"),
                    NormalizeSourceFile(ReadSourceFile(row))
                },
                item => new[]
                {
                    ReadTargetProperty(item, "Item", "GUID"),
                    NormalizeSourceFile(ReadTargetSourceFile(item))
                });
        }

        private static string ReadSourceFile(DataRow row)
        {
            string value = ReadSourceProperty(row, "Item", "Source File Name");
            if (!string.IsNullOrWhiteSpace(value)) return value;
            return ReadSourceProperty(row, "Item", "Source File");
        }

        private static string ReadTargetSourceFile(ModelItem item)
        {
            string value = ReadTargetProperty(item, "Item", "Source File Name");
            if (!string.IsNullOrWhiteSpace(value)) return value;
            return ReadTargetProperty(item, "Item", "Source File");
        }

        private static string ReadSourceProperty(DataRow row, string categoryName, string propertyName)
        {
            if (row == null || row.Table == null) return null;

            string qualifiedName = categoryName + "." + propertyName;
            foreach (DataColumn column in row.Table.Columns)
            {
                if (!string.Equals(column.ColumnName, qualifiedName, StringComparison.OrdinalIgnoreCase))
                    continue;

                return NormalizeVariantString(row[column]);
            }
            return null;
        }

        private static string ReadTargetProperty(ModelItem item, string categoryName, string propertyName)
        {
            if (item == null || item.PropertyCategories == null) return null;

            foreach (PropertyCategory category in item.PropertyCategories)
            {
                if (category == null ||
                    !string.Equals(category.DisplayName, categoryName, StringComparison.OrdinalIgnoreCase) ||
                    category.Properties == null)
                    continue;

                foreach (DataProperty property in category.Properties)
                {
                    if (property == null ||
                        !string.Equals(property.DisplayName, propertyName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    try
                    {
                        return property.Value == null
                            ? null
                            : NormalizeVariantString(property.Value.ToDisplayString());
                    }
                    catch
                    {
                        return null;
                    }
                }
            }
            return null;
        }

        internal static string DescribeSourceRow(DataRow row)
        {
            return "Item.GUID=" + LogValue(ReadSourceProperty(row, "Item", "GUID"))
                + ", Item.SourceFileRaw=" + LogValue(ReadSourceFile(row))
                + ", Item.SourceFileNormalized=" + LogValue(NormalizeSourceFile(ReadSourceFile(row)))
                + ", RID=" + LogValue(ReadSourceProperty(row, "MAE-4D", "RID"));
        }

        internal static string DescribeTargetItem(ModelItem item)
        {
            return "Item.GUID=" + LogValue(ReadTargetProperty(item, "Item", "GUID"))
                + ", Item.SourceFileRaw=" + LogValue(ReadTargetSourceFile(item))
                + ", Item.SourceFileNormalized=" + LogValue(NormalizeSourceFile(ReadTargetSourceFile(item)))
                + ", API.InstanceGuid=" + LogValue(item == null ? null : item.InstanceGuid.ToString("D"));
        }

        private static string LogValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "<null>" : "'" + value + "'";
        }

        private static string NormalizeVariantString(object value)
        {
            if (value == null || value == DBNull.Value) return null;
            string text = Convert.ToString(value);
            if (string.IsNullOrWhiteSpace(text)) return null;
            return text.Trim().Trim('"', '\'');
        }

        private static string NormalizeSourceFile(string value)
        {
            value = NormalizeVariantString(value);
            if (value == null) return null;

            string normalized = value.Replace('/', '\\').TrimEnd('\\');
            int separator = normalized.LastIndexOf('\\');
            if (separator >= 0 && separator < normalized.Length - 1)
                normalized = normalized.Substring(separator + 1);

            return normalized.Trim();
        }
    }
}



