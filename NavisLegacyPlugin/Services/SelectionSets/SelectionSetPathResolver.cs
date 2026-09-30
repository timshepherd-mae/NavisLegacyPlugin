using Autodesk.Navisworks.Api;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace NavisLegacyPlugin.Services.SelectionSets
{
    public sealed class SelectionSetPathResolver
    {
        public IReadOnlyList<ModelItem> ResolveRequired(
            Document document,
            IEnumerable<string> path)
        {
            if (document == null)
            {
                throw new InvalidOperationException(
                    "No active document.");
            }

            if (path == null)
            {
                throw new ArgumentNullException(
                    "path");
            }

            string[] parts =
                path
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToArray();

            if (parts.Length == 0)
            {
                throw new ArgumentException(
                    "A Selection Set path is required.",
                    "path");
            }

            SavedItem current = null;

            SavedItemCollection children =
                document.SelectionSets.RootItem.Children;

            for (int index = 0;
                index < parts.Length;
                index++)
            {
                string part = parts[index];

                if (children == null)
                {
                    throw new InvalidOperationException(
                        string.Format(
                            "'{0}' is not a Selection Set folder.",
                            string.Join(
                                "/",
                                parts.Take(index).ToArray())));
                }

                List<SavedItem> matches =
                    children
                        .Cast<SavedItem>()
                        .Where(
                            x =>
                                string.Equals(
                                    x.DisplayName,
                                    part,
                                    StringComparison.OrdinalIgnoreCase))
                        .ToList();

                if (matches.Count == 0)
                {
                    throw new InvalidOperationException(
                        string.Format(
                            "Selection Set '{0}' was not found.",
                            string.Join("/", parts)));
                }

                if (matches.Count > 1)
                {
                    throw new InvalidOperationException(
                        string.Format(
                            "Selection Set path '{0}' is ambiguous because '{1}' exists more than once.",
                            string.Join("/", parts),
                            part));
                }

                current = matches[0];

                bool isFinalPart =
                    index == parts.Length - 1;

                if (!isFinalPart)
                {
                    GroupItem group =
                        current as GroupItem;

                    if (group == null)
                    {
                        throw new InvalidOperationException(
                            string.Format(
                                "'{0}' must be a Selection Set folder.",
                                string.Join(
                                    "/",
                                    parts.Take(index + 1).ToArray())));
                    }

                    children = group.Children;
                }
            }

            SelectionSet selectionSet =
                current as SelectionSet;

            if (selectionSet == null)
            {
                throw new InvalidOperationException(
                    string.Format(
                        "'{0}' is not a Selection Set.",
                        string.Join("/", parts)));
            }

            List<ModelItem> rawItems =
                selectionSet
                    .GetSelectedItems(document)
                    .Cast<ModelItem>()
                    .ToList();

            // Phase 6.5A2: InstanceGuid is a matching attribute, not a
            // globally unique population identity. Preserve every item returned
            // by the Selection Set, including copied/federated items that share
            // an InstanceGuid.
            List<ModelItem> items = rawItems.ToList();

            if (string.Equals(
                parts[parts.Length - 1],
                "TARGET",
                StringComparison.OrdinalIgnoreCase))
            {
                WriteRawTargetDiagnostics(
                    string.Join("/", parts),
                    selectionSet,
                    rawItems,
                    items);
            }

            if (items.Count == 0)
            {
                throw new InvalidOperationException(
                    string.Format(
                        "Selection Set '{0}' contains no items.",
                        string.Join("/", parts)));
            }

            return items;
        }

        private static void WriteRawTargetDiagnostics(
            string resolvedPath,
            SelectionSet selectionSet,
            IList<ModelItem> rawItems,
            IList<ModelItem> returnedItems)
        {
            string logPath = Path.Combine(
                Path.GetTempPath(),
                "NavisLegacy_Phase65A_selectionset_"
                + Guid.NewGuid().ToString("N")
                + ".selectionset65a.log");

            try
            {
                using (StreamWriter log = new StreamWriter(
                    logPath,
                    false,
                    new UTF8Encoding(true)))
                {
                    log.AutoFlush = true;
                    WriteLog(log, "RAW TARGET DIAGNOSTICS STARTED");
                    WriteLog(log, "Resolved path='" + resolvedPath + "'.");
                    WriteLog(log, "SelectionSet name=" + Format(selectionSet.DisplayName) + ".");
                    LogItems(log, "RAW GetSelectedItems", rawItems);
                    LogItems(log, "RETURNED after InstanceGuid GroupBy", returnedItems);
                    WriteLog(log, "RAW TARGET DIAGNOSTICS FINISHED");
                }

                Debug.WriteLine(
                    "[MATCH65A] SELECTIONSET diagnostics file='"
                    + logPath + "'.");
            }
            catch (Exception exception)
            {
                Debug.WriteLine(
                    "[MATCH65A] SELECTIONSET diagnostics failed for '"
                    + logPath + "': " + exception);
            }
        }

        private static void LogItems(
            StreamWriter log,
            string stage,
            IList<ModelItem> items)
        {
            int count = items == null ? 0 : items.Count;
            WriteLog(log, stage + " count=" + count + ".");

            if (items == null)
                return;

            int duplicateGroups = items
                .Where(x => x != null)
                .GroupBy(x => x.InstanceGuid)
                .Count(x => x.Count() > 1);

            WriteLog(
                log,
                stage + " duplicate InstanceGuid groups="
                + duplicateGroups + ".");

            for (int index = 0; index < items.Count; index++)
            {
                ModelItem item = items[index];
                WriteLog(
                    log,
                    stage + " item " + (index + 1) + "/" + items.Count
                    + ": " + Describe(item) + ".");
            }
        }

        private static string Describe(ModelItem item)
        {
            if (item == null)
                return "item=<null>";

            return "API.InstanceGuid=" + Format(item.InstanceGuid.ToString("D"))
                + ", Item.GUID=" + Format(ReadProperty(item, "Item", "GUID"))
                + ", Item.SourceFile=" + Format(ReadSourceFile(item))
                + ", RID=" + Format(ReadProperty(item, "MAE-4D", "RID"))
                + ", DisplayName=" + Format(item.DisplayName);
        }

        private static string ReadSourceFile(ModelItem item)
        {
            string value = ReadProperty(item, "Item", "Source File Name");
            return string.IsNullOrWhiteSpace(value)
                ? ReadProperty(item, "Item", "Source File")
                : value;
        }

        private static string ReadProperty(
            ModelItem item,
            string categoryName,
            string propertyName)
        {
            if (item == null || item.PropertyCategories == null)
                return null;

            foreach (PropertyCategory category in item.PropertyCategories)
            {
                if (category == null
                    || category.Properties == null
                    || !string.Equals(category.DisplayName, categoryName,
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                foreach (DataProperty property in category.Properties)
                {
                    if (property == null
                        || !string.Equals(property.DisplayName, propertyName,
                            StringComparison.OrdinalIgnoreCase))
                        continue;

                    try
                    {
                        if (property.Value != null && property.Value.IsDisplayString)
                            return property.Value.ToDisplayString();

                        return property.Value == null
                            ? null
                            : property.Value.ToString();
                    }
                    catch (Exception exception)
                    {
                        return "<unreadable value: "
                            + exception.GetType().Name + ">";
                    }
                }
            }

            return null;
        }

        private static void WriteLog(StreamWriter log, string message)
        {
            string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
                + " [MATCH65A] SELECTIONSET " + message;
            Debug.WriteLine(line);
            log.WriteLine(line);
        }

        private static string Format(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "<null>"
                : "'" + value + "'";
        }

    }
}

