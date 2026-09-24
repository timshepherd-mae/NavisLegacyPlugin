using System;
using System.Collections.Generic;
using System.Data;
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
                throw new InvalidOperationException("External SOURCE response contains no item collection.");

            var identities = new HashSet<Guid>();
            foreach (ExternalModelItemSnapshot item in response.Items)
            {
                Guid identity;
                if (item == null || !Guid.TryParse(item.InstanceGuid, out identity))
                    throw new InvalidOperationException("External SOURCE contains an item with an invalid InstanceGuid.");
                if (!identities.Add(identity))
                    throw new InvalidOperationException("External SOURCE contains duplicate InstanceGuid values.");
            }

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
            return table;
        }

        private static string GetColumnName(ExternalPropertySnapshot property)
        {
            if (property == null || string.IsNullOrWhiteSpace(property.Category) ||
                string.IsNullOrWhiteSpace(property.Name))
                return null;
            return property.Category.Trim() + "." + property.Name.Trim();
        }
    }
}
