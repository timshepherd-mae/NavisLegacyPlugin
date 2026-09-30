using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using NavisLegacyPlugin.Models.ExternalSources;

namespace NavisLegacyPlugin.Services.ExternalSources
{
    /// <summary>
    /// Adapts the Phase 6.3 transport response into host-side SOURCE data without
    /// introducing ModelItem instances across the Automation process boundary.
    /// </summary>
    public sealed class ExternalSourcePopulationAdapter
    {
        public ExternalSourcePopulation Adapt(ExternalExportPopulationResponse response)
        {
            if (response == null)
                throw new ArgumentNullException("response");
            if (!response.Success)
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(response.Error)
                        ? "External SOURCE extraction failed."
                        : response.Error);
            if (response.Items == null)
                throw new InvalidOperationException(
                    "External SOURCE response contains no item collection.");

            Debug.WriteLine(
                "[MATCH65A] ADAPTER INPUT response items="
                + response.Items.Count + ".");

            for (int itemIndex = 0; itemIndex < response.Items.Count; itemIndex++)
            {
                ExternalModelItemSnapshot item = response.Items[itemIndex];

                Debug.WriteLine(
                    "[MATCH65A] ADAPTER INPUT item "
                    + (itemIndex + 1) + "/" + response.Items.Count
                    + ": Snapshot.InstanceGuid="
                    + FormatLogValue(item == null ? null : item.InstanceGuid)
                    + ", Item.GUID="
                    + FormatLogValue(ReadProperty(item, "Item", "GUID"))
                    + ", Item.SourceFile="
                    + FormatLogValue(ReadSourceFile(item))
                    + ", RID="
                    + FormatLogValue(ReadProperty(item, "MAE-4D", "RID"))
                    + ", DisplayName="
                    + FormatLogValue(item == null ? null : item.DisplayName)
                    + ".");
            }

            var identities = new HashSet<Guid>();
            int duplicateIdentityCount = 0;

            foreach (ExternalModelItemSnapshot item in response.Items)
            {
                Guid identity;
                if (item == null || !Guid.TryParse(item.InstanceGuid, out identity))
                {
                    throw new InvalidOperationException(
                        "External SOURCE contains an item with an invalid InstanceGuid.");
                }

                if (!identities.Add(identity))
                {
                    duplicateIdentityCount++;
                    Debug.WriteLine(
                        "[MATCH65A] ADAPTER INPUT duplicate Snapshot.InstanceGuid="
                        + identity.ToString("D") + ".");

                    // A repeated InstanceGuid is valid in federated/instanced models.
                    // Preserve every snapshot; ordered matching is responsible for
                    // resolving ambiguity through composite and fallback strategies.
                }
            }

            Debug.WriteLine(
                "[MATCH65A] ADAPTER INPUT unique InstanceGuids="
                + identities.Count
                + ", duplicate count="
                + duplicateIdentityCount
                + ".");

            return new ExternalSourcePopulation(
                response.SourceFile,
                response.ExportSetName,
                response.ResolutionType,
                response.Items.AsReadOnly(),
                identities.ToList().AsReadOnly());
        }

        public DataTable ToDataTable(ExternalSourcePopulation population)
        {
            if (population == null)
                throw new ArgumentNullException("population");

            var table = new DataTable();
            table.CaseSensitive = false;
            table.Columns.Add("InstanceGuid", typeof(string));

            foreach (ExternalModelItemSnapshot item in population.Items)
            {
                if (item == null || item.Properties == null)
                    continue;

                foreach (ExternalPropertySnapshot property in item.Properties)
                {
                    string columnName = GetColumnName(property);
                    if (columnName != null && !table.Columns.Contains(columnName))
                        table.Columns.Add(columnName, typeof(string));
                }
            }

            foreach (ExternalModelItemSnapshot item in population.Items)
            {
                if (item == null)
                    continue;

                DataRow row = table.NewRow();
                row["InstanceGuid"] = item.InstanceGuid ?? string.Empty;

                if (item.Properties != null)
                {
                    foreach (ExternalPropertySnapshot property in item.Properties)
                    {
                        string columnName = GetColumnName(property);
                        if (columnName != null && row.IsNull(columnName))
                            row[columnName] = property.Value ?? string.Empty;
                    }
                }

                table.Rows.Add(row);
            }

            Debug.WriteLine(
                "[MATCH65A] DATATABLE rows="
                + table.Rows.Count
                + ", columns="
                + table.Columns.Count
                + ".");

            return table;
        }

        private static string ReadSourceFile(ExternalModelItemSnapshot item)
        {
            string value = ReadProperty(item, "Item", "Source File Name");
            if (!string.IsNullOrWhiteSpace(value))
                return value;

            return ReadProperty(item, "Item", "Source File");
        }

        private static string ReadProperty(
            ExternalModelItemSnapshot item,
            string categoryName,
            string propertyName)
        {
            if (item == null || item.Properties == null)
                return null;

            foreach (ExternalPropertySnapshot property in item.Properties)
            {
                if (property == null)
                    continue;

                if (string.Equals(
                        property.Category,
                        categoryName,
                        StringComparison.OrdinalIgnoreCase)
                    && string.Equals(
                        property.Name,
                        propertyName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return property.Value;
                }
            }

            return null;
        }

        private static string FormatLogValue(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "<null>"
                : "'" + value + "'";
        }

        private static string GetColumnName(ExternalPropertySnapshot property)
        {
            if (property == null
                || string.IsNullOrWhiteSpace(property.Category)
                || string.IsNullOrWhiteSpace(property.Name))
            {
                return null;
            }

            return property.Category.Trim()
                + "."
                + property.Name.Trim();
        }
    }
}