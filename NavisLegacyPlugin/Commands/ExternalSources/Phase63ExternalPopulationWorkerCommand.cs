using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Plugins;
using NavisLegacyPlugin.Models.Collections;
using NavisLegacyPlugin.Models.ExternalSources;
using NavisLegacyPlugin.Services.Collections;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;

namespace NavisLegacyPlugin.Commands.ExternalSources
{
    [Plugin("Phase63ExternalPopulationWorker", "MAE", DisplayName = "Phase 6.3 External Population Worker")]
    public sealed class Phase63ExternalPopulationWorkerCommand : AddInPlugin
    {
        public override int Execute(params string[] parameters)
        {
            if (parameters == null || parameters.Length < 5 || parameters[0] != "WORKER")
                return -1;

            string output = parameters[1];
            string source = parameters[2];
            string exportName = parameters[3];
            string resolutionName = parameters[4];
            string logPath = output + ".match65a.log";

            ExternalExportPopulationResponse response = new ExternalExportPopulationResponse
            {
                SourceFile = source,
                ExportSetName = exportName,
                ResolutionType = resolutionName
            };

            using (StreamWriter log = CreateWorkerLog(logPath))
            {
                WriteLog(log, "WORKER STARTED");
                WriteLog(log, "Response file=" + FormatLogValue(output) + ".");
                WriteLog(log, "Worker log file=" + FormatLogValue(logPath) + ".");
                WriteLog(log, "Source file=" + FormatLogValue(source) + ".");
                WriteLog(log, "Export set=" + FormatLogValue(exportName) + ".");
                WriteLog(log, "Resolution=" + FormatLogValue(resolutionName) + ".");

                try
                {
                    response.SourceHashBefore = Hash(source);
                    WriteLog(log, "Source hash before=" + response.SourceHashBefore + ".");

                    Document document = Autodesk.Navisworks.Api.Application.ActiveDocument;
                    if (document == null)
                        throw new InvalidOperationException("Worker ActiveDocument is null.");

                    SelectionSet set = FindExportSet(document, exportName);
                    WriteLog(log, "Export set found=" + FormatLogValue(set.DisplayName) + ".");

                    ModelItemCollection selected = set.GetSelectedItems();
                    WriteLog(log, "SELECTED ROOT count=" + selected.Count + ".");
                    LogItems(log, "SELECTED ROOT", selected);

                    ModelItemCollectionResolver resolver = new ModelItemCollectionResolver();
                    CollectionResolutionResult resolved = resolver.Resolve(selected);

                    WriteLog(
                        log,
                        "RESOLVED counts: All=" + resolved.AllItems.Count
                        + ", Branch=" + resolved.BranchItems.Count
                        + ", Leaf=" + resolved.LeafItems.Count + ".");

                    LogItems(log, "RESOLVED ALL", resolved.AllItems);
                    LogItems(log, "RESOLVED BRANCH", resolved.BranchItems);
                    LogItems(log, "RESOLVED LEAF", resolved.LeafItems);

                    response.AllCount = resolved.AllItems.Count;
                    response.BranchCount = resolved.BranchItems.Count;
                    response.LeafCount = resolved.LeafItems.Count;

                    CollectionResolutionType type;
                    if (!Enum.TryParse(resolutionName, true, out type))
                        throw new InvalidOperationException("Invalid resolution type: " + resolutionName);

                    IEnumerable<ModelItem> outputItems =
                        type == CollectionResolutionType.Branch
                            ? resolved.BranchItems
                            : type == CollectionResolutionType.Leaf
                                ? resolved.LeafItems
                                : resolved.AllItems;

                    List<ModelItem> outputItemList = outputItems
                        .Where(item => item != null)
                        .ToList();

                    WriteLog(
                        log,
                        "OUTPUT resolution=" + type
                        + ", count=" + outputItemList.Count + ".");
                    LogItems(log, "OUTPUT", outputItemList);

                    foreach (ModelItem item in outputItemList)
                    {
                        ExternalModelItemSnapshot snapshot = new ExternalModelItemSnapshot
                        {
                            InstanceGuid = item.InstanceGuid.ToString(),
                            DisplayName = item.DisplayName,
                            HasChildren = item.Children.Any()
                        };

                        foreach (PropertyCategory category in item.PropertyCategories)
                        {
                            foreach (DataProperty property in category.Properties)
                            {
                                snapshot.Properties.Add(new ExternalPropertySnapshot
                                {
                                    Category = category.DisplayName,
                                    Name = property.DisplayName,
                                    Value = SerializeVariantData(property.Value)
                                });
                            }
                        }

                        response.Items.Add(snapshot);
                    }

                    WriteLog(log, "RESPONSE snapshots=" + response.Items.Count + ".");

                    response.SourceHashAfter = Hash(source);
                    WriteLog(log, "Source hash after=" + response.SourceHashAfter + ".");

                    if (!string.Equals(
                        response.SourceHashBefore,
                        response.SourceHashAfter,
                        StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            "READ-ONLY VIOLATION: source hash changed during extraction.");
                    }

                    response.Success = true;
                    WriteLog(log, "WORKER processing SUCCESS.");
                }
                catch (Exception exception)
                {
                    response.Success = false;
                    response.Error = exception.ToString();
                    WriteLog(log, "WORKER EXCEPTION: " + exception);

                    try
                    {
                        response.SourceHashAfter = Hash(source);
                        WriteLog(log, "Source hash after exception=" + response.SourceHashAfter + ".");
                    }
                    catch (Exception hashException)
                    {
                        WriteLog(log, "Source hash after exception could not be read: " + hashException);
                    }
                }

                try
                {
                    using (FileStream stream = File.Create(output))
                    {
                        DataContractJsonSerializer serializer =
                            new DataContractJsonSerializer(typeof(ExternalExportPopulationResponse));
                        serializer.WriteObject(stream, response);
                    }

                    WriteLog(
                        log,
                        "Response JSON written=" + FormatLogValue(output)
                        + ", success=" + response.Success + ".");
                }
                catch (Exception serializationException)
                {
                    WriteLog(log, "RESPONSE SERIALIZATION EXCEPTION: " + serializationException);
                    return -1;
                }

                WriteLog(log, "WORKER FINISHED return=" + (response.Success ? 0 : -1) + ".");
                return response.Success ? 0 : -1;
            }
        }

        private static StreamWriter CreateWorkerLog(string path)
        {
            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                StreamWriter writer = new StreamWriter(
                    path,
                    false,
                    new UTF8Encoding(true));
                writer.AutoFlush = true;
                return writer;
            }
            catch (Exception exception)
            {
                Debug.WriteLine(
                    "[MATCH65A] WORKER could not create file log '"
                    + path + "': " + exception);
                return null;
            }
        }

        private static void WriteLog(StreamWriter log, string message)
        {
            string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
                + " [MATCH65A] WORKER " + message;

            Debug.WriteLine(line);

            if (log == null)
                return;

            try
            {
                log.WriteLine(line);
            }
            catch (Exception exception)
            {
                Debug.WriteLine(
                    "[MATCH65A] WORKER file-log write failed: " + exception);
            }
        }

        private static SelectionSet FindExportSet(Document document, string name)
        {
            SavedItemCollection children = document.SelectionSets.RootItem.Children;

            foreach (string part in new[] { "MAE-4D", "DATA-TRANSFER", "EXPORT" })
            {
                List<SavedItem> matches = children
                    .Cast<SavedItem>()
                    .Where(x => string.Equals(
                        x.DisplayName,
                        part,
                        StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (matches.Count != 1 || !(matches[0] is GroupItem))
                    throw new InvalidOperationException(
                        "Required folder missing or ambiguous: " + part);

                children = ((GroupItem)matches[0]).Children;
            }

            List<SelectionSet> sets = children
                .Cast<SavedItem>()
                .OfType<SelectionSet>()
                .Where(x => string.Equals(
                    x.DisplayName,
                    name,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (sets.Count != 1)
                throw new InvalidOperationException(
                    "Export set missing or ambiguous: " + name);

            return sets[0];
        }

        private static void LogItems(
            StreamWriter log,
            string stage,
            IEnumerable<ModelItem> items)
        {
            if (items == null)
            {
                WriteLog(log, stage + " collection=<null>.");
                return;
            }

            int index = 0;
            foreach (ModelItem item in items)
            {
                index++;
                WriteLog(
                    log,
                    stage + " item " + index
                    + ": API.InstanceGuid=" + FormatLogValue(
                        item == null ? null : item.InstanceGuid.ToString("D"))
                    + ", Item.GUID=" + FormatLogValue(
                        ReadItemProperty(item, "GUID"))
                    + ", Item.SourceFile=" + FormatLogValue(
                        ReadSourceFile(item))
                    + ", RID=" + FormatLogValue(
                        ReadProperty(item, "MAE-4D", "RID"))
                    + ", DisplayName=" + FormatLogValue(
                        item == null ? null : item.DisplayName)
                    + ".");
            }

            WriteLog(log, stage + " enumerated count=" + index + ".");
        }

        private static string ReadSourceFile(ModelItem item)
        {
            string value = ReadItemProperty(item, "Source File Name");
            if (!string.IsNullOrWhiteSpace(value))
                return value;

            return ReadItemProperty(item, "Source File");
        }

        private static string ReadItemProperty(ModelItem item, string propertyName)
        {
            return ReadProperty(item, "Item", propertyName);
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
                    || !string.Equals(
                        category.DisplayName,
                        categoryName,
                        StringComparison.OrdinalIgnoreCase)
                    || category.Properties == null)
                {
                    continue;
                }

                foreach (DataProperty property in category.Properties)
                {
                    if (property == null
                        || !string.Equals(
                            property.DisplayName,
                            propertyName,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    return SerializeVariantData(property.Value);
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

        private static string SerializeVariantData(VariantData value)
        {
            if (value == null)
                return null;

            try
            {
                if (value.IsDisplayString)
                    return value.ToDisplayString();
            }
            catch (Exception exception)
            {
                return "<unreadable display value: " + exception.GetType().Name + ">";
            }

            try
            {
                string fallback = value.ToString();
                return string.IsNullOrEmpty(fallback)
                    ? "<non-display value>"
                    : fallback;
            }
            catch (Exception exception)
            {
                return "<unreadable value: " + exception.GetType().Name + ">";
            }
        }

        private static string Hash(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete))
            {
                return BitConverter
                    .ToString(sha.ComputeHash(stream))
                    .Replace("-", string.Empty);
            }
        }
    }
}
