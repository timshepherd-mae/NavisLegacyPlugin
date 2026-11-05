using System;
using System.Collections.Generic;
using System.Data;
using Autodesk.Navisworks.Api;

namespace NavisLegacyPlugin.Services.Matching
{
    /// <summary>
    /// Creates the supported SOURCE-to-TARGET matching strategies.
    ///
    /// Each returned strategy represents one explicit anchor method.
    /// Strategies do not silently fall back to another identifier when
    /// a required component is missing.
    /// </summary>
    public static class MatchStrategyFactory
    {
        /// <summary>
        /// Creates an Item.GUID-only matching strategy.
        /// </summary>
        public static MatchStrategyDefinition ItemGuid(int priority)
        {
            return new MatchStrategyDefinition(
                "Item.GUID",
                priority,
                true,
                row => new[]
                {
                    ReadSourceProperty(
                        row,
                        "Item",
                        "GUID")
                },
                item => new[]
                {
                    ReadTargetProperty(
                        item,
                        "Item",
                        "GUID")
                });
        }

        /// <summary>
        /// Creates the Item.GUID plus normalized filename strategy.
        ///
        /// This is the preferred user-facing name for the existing
        /// Item.GUID plus Item.SourceFile matching behaviour.
        /// </summary>
        public static MatchStrategyDefinition ItemGuidAndFilename(
            int priority)
        {
            return new MatchStrategyDefinition(
                "Item.GUID + Filename",
                priority,
                true,
                row => new[]
                {
                    ReadSourceProperty(
                        row,
                        "Item",
                        "GUID"),
                    NormalizeFilename(
                        ReadSourceFile(row))
                },
                item => new[]
                {
                    ReadTargetProperty(
                        item,
                        "Item",
                        "GUID"),
                    NormalizeFilename(
                        ReadTargetSourceFile(item))
                });
        }

        /// <summary>
        /// Compatibility entry point retained for existing callers.
        ///
        /// The matching behaviour is identical to ItemGuidAndFilename.
        /// New configuration code should use ItemGuidAndFilename.
        /// </summary>
        public static MatchStrategyDefinition ItemGuidAndSourceFile(
            int priority)
        {
            return ItemGuidAndFilename(priority);
        }

        /// <summary>
        /// Creates an IfcGuid-only matching strategy.
        ///
        /// IfcGuid is searched by property display name across all
        /// categories because its containing category can vary between
        /// imported model formats.
        /// </summary>
        public static MatchStrategyDefinition IfcGuid(int priority)
        {
            return new MatchStrategyDefinition(
                "IfcGuid",
                priority,
                true,
                row => new[]
                {
                    ReadSourcePropertyByName(
                        row,
                        "IfcGuid")
                },
                item => new[]
                {
                    ReadTargetPropertyByName(
                        item,
                        "IfcGuid")
                });
        }

        /// <summary>
        /// Creates an IfcGuid plus ModelItem DisplayName strategy.
        /// Both components are required to construct a usable key.
        /// </summary>
        public static MatchStrategyDefinition IfcGuidAndDisplayName(
            int priority)
        {
            return new MatchStrategyDefinition(
                "IfcGuid + DisplayName",
                priority,
                true,
                row => new[]
                {
                    ReadSourcePropertyByName(
                        row,
                        "IfcGuid"),
                    ReadSourceDisplayName(row)
                },
                item => new[]
                {
                    ReadTargetPropertyByName(
                        item,
                        "IfcGuid"),
                    NormalizeVariantString(
                        item == null
                            ? null
                            : item.DisplayName)
                });
        }

        /// <summary>
        /// Reads the SOURCE filename using the existing supported
        /// Item property-name alternatives.
        /// </summary>
        private static string ReadSourceFile(DataRow row)
        {
            string value = ReadSourceProperty(
                row,
                "Item",
                "Source File Name");

            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            return ReadSourceProperty(
                row,
                "Item",
                "Source File");
        }

        /// <summary>
        /// Reads the TARGET filename using the existing supported
        /// Item property-name alternatives.
        /// </summary>
        private static string ReadTargetSourceFile(ModelItem item)
        {
            string value = ReadTargetProperty(
                item,
                "Item",
                "Source File Name");

            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            return ReadTargetProperty(
                item,
                "Item",
                "Source File");
        }

        /// <summary>
        /// Reads an explicitly qualified SOURCE property column.
        /// Expected column format is Category.Property.
        /// </summary>
        private static string ReadSourceProperty(
            DataRow row,
            string categoryName,
            string propertyName)
        {
            if (row == null ||
                row.Table == null ||
                string.IsNullOrWhiteSpace(categoryName) ||
                string.IsNullOrWhiteSpace(propertyName))
            {
                return null;
            }

            string qualifiedName =
                categoryName.Trim() +
                "." +
                propertyName.Trim();

            foreach (DataColumn column in row.Table.Columns)
            {
                if (column == null ||
                    !string.Equals(
                        column.ColumnName,
                        qualifiedName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return NormalizeVariantString(row[column]);
            }

            return null;
        }

        /// <summary>
        /// Reads a SOURCE property by unqualified property name.
        ///
        /// Exact unqualified columns are preferred. Qualified
        /// Category.Property columns are then searched by their final
        /// property-name component.
        /// </summary>
        private static string ReadSourcePropertyByName(
            DataRow row,
            string propertyName)
        {
            if (row == null ||
                row.Table == null ||
                string.IsNullOrWhiteSpace(propertyName))
            {
                return null;
            }

            string requiredName = propertyName.Trim();

            foreach (DataColumn column in row.Table.Columns)
            {
                if (column == null ||
                    !string.Equals(
                        column.ColumnName,
                        requiredName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string value =
                    NormalizeVariantString(row[column]);

                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            foreach (DataColumn column in row.Table.Columns)
            {
                if (column == null ||
                    !IsQualifiedPropertyMatch(
                        column.ColumnName,
                        requiredName))
                {
                    continue;
                }

                string value =
                    NormalizeVariantString(row[column]);

                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return null;
        }

        /// <summary>
        /// Reads the transported SOURCE display name.
        ///
        /// The adapter may expose this as DisplayName, Item.DisplayName,
        /// Item.Display Name or Item.Name. The aliases are checked
        /// explicitly and no unrelated property is used as a fallback.
        /// </summary>
        private static string ReadSourceDisplayName(DataRow row)
        {
            string value = ReadSourceColumn(
                row,
                "DisplayName");

            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            value = ReadSourceColumn(
                row,
                "Item.DisplayName");

            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            value = ReadSourceColumn(
                row,
                "Item.Display Name");

            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            return ReadSourceColumn(
                row,
                "Item.Name");
        }

        /// <summary>
        /// Reads one SOURCE column using a case-insensitive column name.
        /// </summary>
        private static string ReadSourceColumn(
            DataRow row,
            string columnName)
        {
            if (row == null ||
                row.Table == null ||
                string.IsNullOrWhiteSpace(columnName))
            {
                return null;
            }

            foreach (DataColumn column in row.Table.Columns)
            {
                if (column == null ||
                    !string.Equals(
                        column.ColumnName,
                        columnName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return NormalizeVariantString(row[column]);
            }

            return null;
        }

        /// <summary>
        /// Reads a TARGET property from a specified category.
        /// </summary>
        private static string ReadTargetProperty(
            ModelItem item,
            string categoryName,
            string propertyName)
        {
            if (item == null ||
                item.PropertyCategories == null ||
                string.IsNullOrWhiteSpace(categoryName) ||
                string.IsNullOrWhiteSpace(propertyName))
            {
                return null;
            }

            foreach (PropertyCategory category
                in item.PropertyCategories)
            {
                if (category == null ||
                    category.Properties == null ||
                    !CategoryNameMatches(
                        category,
                        categoryName))
                {
                    continue;
                }

                foreach (DataProperty property
                    in category.Properties)
                {
                    if (property == null ||
                        !PropertyNameMatches(
                            property,
                            propertyName))
                    {
                        continue;
                    }

                    return ReadTargetPropertyValue(property);
                }
            }

            return null;
        }

        /// <summary>
        /// Reads a TARGET property by property name across every category.
        ///
        /// This is required for properties such as IfcGuid whose category
        /// can differ between imported model formats.
        /// </summary>
        private static string ReadTargetPropertyByName(
            ModelItem item,
            string propertyName)
        {
            if (item == null ||
                item.PropertyCategories == null ||
                string.IsNullOrWhiteSpace(propertyName))
            {
                return null;
            }

            foreach (PropertyCategory category
                in item.PropertyCategories)
            {
                if (category == null ||
                    category.Properties == null)
                {
                    continue;
                }

                foreach (DataProperty property
                    in category.Properties)
                {
                    if (property == null ||
                        !PropertyNameMatches(
                            property,
                            propertyName))
                    {
                        continue;
                    }

                    string value =
                        ReadTargetPropertyValue(property);

                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Compares the requested category against both display and
        /// internal category names.
        /// </summary>
        private static bool CategoryNameMatches(
            PropertyCategory category,
            string categoryName)
        {
            if (category == null ||
                string.IsNullOrWhiteSpace(categoryName))
            {
                return false;
            }

            return string.Equals(
                       category.DisplayName,
                       categoryName,
                       StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(
                       category.Name,
                       categoryName,
                       StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Compares the requested property against both display and
        /// internal property names.
        /// </summary>
        private static bool PropertyNameMatches(
            DataProperty property,
            string propertyName)
        {
            if (property == null ||
                string.IsNullOrWhiteSpace(propertyName))
            {
                return false;
            }

            return string.Equals(
                       property.DisplayName,
                       propertyName,
                       StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(
                       property.Name,
                       propertyName,
                       StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Safely converts a Navisworks property value into a normalized
        /// matching component.
        /// </summary>
        private static string ReadTargetPropertyValue(
            DataProperty property)
        {
            if (property == null ||
                property.Value == null)
            {
                return null;
            }

            try
            {
                return NormalizeVariantString(
                    property.Value.ToDisplayString());
            }
            catch
            {
                try
                {
                    return NormalizeVariantString(
                        property.Value.ToString());
                }
                catch
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Determines whether a qualified SOURCE column ends with the
        /// requested property name.
        /// </summary>
        private static bool IsQualifiedPropertyMatch(
            string columnName,
            string propertyName)
        {
            if (string.IsNullOrWhiteSpace(columnName) ||
                string.IsNullOrWhiteSpace(propertyName))
            {
                return false;
            }

            int separatorIndex =
                columnName.LastIndexOf('.');

            if (separatorIndex < 0 ||
                separatorIndex >= columnName.Length - 1)
            {
                return false;
            }

            string unqualifiedName =
                columnName.Substring(separatorIndex + 1);

            return string.Equals(
                unqualifiedName,
                propertyName,
                StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Converts SOURCE or TARGET values into normalized strategy
        /// components. Final case normalization remains the responsibility
        /// of MatchKey.Create.
        /// </summary>
        private static string NormalizeVariantString(
            object value)
        {
            if (value == null ||
                value == DBNull.Value)
            {
                return null;
            }

            string text;

            try
            {
                text = Convert.ToString(value);
            }
            catch
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            text = text.Trim();

            if (text.Length >= 2)
            {
                bool doubleQuoted =
                    text[0] == '"' &&
                    text[text.Length - 1] == '"';

                bool singleQuoted =
                    text[0] == '\'' &&
                    text[text.Length - 1] == '\'';

                if (doubleQuoted || singleQuoted)
                {
                    text = text
                        .Substring(1, text.Length - 2)
                        .Trim();
                }
            }

            return string.IsNullOrWhiteSpace(text)
                ? null
                : text;
        }

        /// <summary>
        /// Normalizes a source-file value to filename-only comparison.
        ///
        /// Directory paths are deliberately ignored so that full paths
        /// and filename-only values can match.
        /// </summary>
        private static string NormalizeFilename(
            string value)
        {
            value = NormalizeVariantString(value);

            if (value == null)
            {
                return null;
            }

            string normalized =
                value
                    .Replace('/', '\\')
                    .TrimEnd('\\');

            int separator =
                normalized.LastIndexOf('\\');

            if (separator >= 0 &&
                separator < normalized.Length - 1)
            {
                normalized =
                    normalized.Substring(separator + 1);
            }

            normalized = normalized.Trim();

            return string.IsNullOrWhiteSpace(normalized)
                ? null
                : normalized;
        }
    }
}