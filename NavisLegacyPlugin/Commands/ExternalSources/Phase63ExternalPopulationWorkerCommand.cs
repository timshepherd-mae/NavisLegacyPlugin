using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Plugins;
using NavisLegacyPlugin.Models.Collections;
using NavisLegacyPlugin.Models.ExternalSources;
using NavisLegacyPlugin.Services.Collections;

namespace NavisLegacyPlugin.Commands.ExternalSources
{
    [Plugin(
        "Phase63ExternalPopulationWorker",
        "MAE",
        DisplayName = "Phase 6.3 External Population Worker")]
    public sealed class Phase63ExternalPopulationWorkerCommand : AddInPlugin
    {
        private const string RidCategoryName = "MAE-4D";
        private const string RidPropertyName = "RID";
        private const int ContextNodesAboveOwner = 2;

        public override int Execute(params string[] parameters)
        {
            if (parameters == null
                || parameters.Length < 6
                || (parameters[0] != "WORKER" && parameters[0] != "VALIDATE"))
            {
                return -1;
            }

            bool validationOnly = parameters[0] == "VALIDATE";
            string output = parameters[1];
            string source = parameters[2];
            string exportName = parameters[3];
            string resolutionName = parameters[4];
            string progressPath = parameters[5];

            var response = new ExternalExportPopulationResponse
            {
                SourceFile = source,
                ExportSetName = exportName,
                ResolutionType = resolutionName
            };

            try
            {
                PublishProgress(
                    progressPath,
                    ExternalSourceProgressStage.LocatingExportSet,
                    0,
                    0,
                    "Locating SOURCE Selection Set...",
                    false);

                response.SourceHashBefore = Hash(source);

                Document document =
                    Autodesk.Navisworks.Api.Application.ActiveDocument;

                if (document == null)
                {
                    throw new InvalidOperationException(
                        "Worker ActiveDocument is null.");
                }

                SelectionSet set = FindExportSet(document, exportName);

                PublishProgress(
                    progressPath,
                    ExternalSourceProgressStage.ResolvingSourcePopulation,
                    0,
                    0,
                    "Resolving SOURCE population...",
                    false);

                ModelItemCollection selected = set.GetSelectedItems();
                var resolver = new ModelItemCollectionResolver();
                CollectionResolutionResult resolved = resolver.Resolve(selected);

                response.AllCount = resolved.AllItems.Count;
                response.BranchCount = resolved.BranchItems.Count;
                response.LeafCount = resolved.LeafItems.Count;

                CollectionResolutionType type;
                if (!Enum.TryParse(resolutionName, true, out type))
                {
                    throw new InvalidOperationException(
                        "Invalid resolution type: " + resolutionName);
                }

                IEnumerable<ModelItem> outputItems =
                    type == CollectionResolutionType.Branch
                        ? resolved.BranchItems
                        : type == CollectionResolutionType.Leaf
                            ? resolved.LeafItems
                            : resolved.AllItems;

                List<ModelItem> outputItemList = outputItems
                    .Where(item => item != null)
                    .ToList();

                int outputTotal = outputItemList.Count;
                if (outputTotal == 0)
                {
                    throw new InvalidOperationException(
                        "The external EXPORT Selection Set contains no items " +
                        "for the selected " + type + " resolution.");
                }

                if (validationOnly)
                {
                    response.Success = true;
                    response.SourceHashAfter = response.SourceHashBefore;

                    PublishProgress(
                        progressPath,
                        ExternalSourceProgressStage.ExternalSourceReady,
                        outputTotal,
                        outputTotal,
                        "External SOURCE validation complete.",
                        true);

                    throw new ValidationCompletedException();
                }

                int outputCurrent = 0;
                PublishProgress(
                    progressPath,
                    ExternalSourceProgressStage.ReadingSourceData,
                    0,
                    outputTotal,
                    "Reading SOURCE data 0/" + outputTotal + "...",
                    true);

                foreach (ModelItem item in outputItemList)
                {
                    ExternalModelItemSnapshot snapshot = CreateSnapshot(item);
                    response.Items.Add(snapshot);

                    outputCurrent++;
                    if (ShouldPublishItemProgress(outputCurrent, outputTotal))
                    {
                        PublishProgress(
                            progressPath,
                            ExternalSourceProgressStage.ReadingSourceData,
                            outputCurrent,
                            outputTotal,
                            "Reading SOURCE data " + outputCurrent + "/" +
                            outputTotal + "...",
                            true);
                    }
                }

                PublishProgress(
                    progressPath,
                    ExternalSourceProgressStage.VerifyingSource,
                    outputTotal,
                    outputTotal,
                    "Verifying external SOURCE file...",
                    false);

                response.SourceHashAfter = Hash(source);
                if (!string.Equals(
                    response.SourceHashBefore,
                    response.SourceHashAfter,
                    StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "READ-ONLY VIOLATION: source hash changed during extraction.");
                }

                response.Success = true;
                PublishProgress(
                    progressPath,
                    ExternalSourceProgressStage.ExternalSourceReady,
                    outputTotal,
                    outputTotal,
                    "External SOURCE data ready.",
                    true);
            }
            catch (ValidationCompletedException)
            {
            }
            catch (Exception exception)
            {
                response.Success = false;
                response.Error = exception.ToString();

                try
                {
                    response.SourceHashAfter = Hash(source);
                }
                catch
                {
                }
            }

            try
            {
                using (FileStream stream = File.Create(output))
                {
                    var serializer = new DataContractJsonSerializer(
                        typeof(ExternalExportPopulationResponse));
                    serializer.WriteObject(stream, response);
                }
            }
            catch
            {
                return -1;
            }

            return response.Success ? 0 : -1;
        }

        private static ExternalModelItemSnapshot CreateSnapshot(ModelItem item)
        {
            var snapshot = new ExternalModelItemSnapshot
            {
                InstanceGuid = item.InstanceGuid.ToString("D"),
                DisplayName = item.DisplayName,
                HasChildren = HasChildren(item)
            };

            foreach (PropertyCategory category in item.PropertyCategories)
            {
                foreach (DataProperty property in category.Properties)
                {
                    snapshot.Properties.Add(
                        new ExternalPropertySnapshot
                        {
                            Category = category.DisplayName,
                            Name = property.DisplayName,
                            Value = SerializeVariantData(property.Value)
                        });
                }
            }

            List<PathCapture> path = BuildAncestorPath(item);
            snapshot.AncestorPath.AddRange(
                path.Select(value => value.Snapshot));

            PopulateRidOwnerEvidence(snapshot, path);
            return snapshot;
        }

        private static void PopulateRidOwnerEvidence(
            ExternalModelItemSnapshot snapshot,
            IReadOnlyList<PathCapture> path)
        {
            snapshot.HasRidOwner = false;
            snapshot.RidOwnerDepthFromAnchor = -1;
            snapshot.RidOwner = null;
            snapshot.TopologyPath.Clear();

            if (path == null || path.Count == 0)
            {
                return;
            }

            string anchorRid = ReadProperty(
                path[0].Item,
                RidCategoryName,
                RidPropertyName);

            PathCapture owner = null;

            if (IsTransferableRid(anchorRid))
            {
                // A pre-propagated leaf can carry the owner's RID. Select the
                // highest branch in the upward path with the same RID. This
                // recovers the original owner instead of selecting the leaf or
                // the first intermediate descendant carrying the propagated RID.
                owner = path
                    .Where(value =>
                        value.Snapshot.HasChildren
                        && RidEquals(
                            ReadProperty(
                                value.Item,
                                RidCategoryName,
                                RidPropertyName),
                            anchorRid))
                    .OrderByDescending(value => value.Snapshot.DepthFromItem)
                    .FirstOrDefault();

                if (owner == null)
                {
                    owner = path[0];
                }
            }
            else
            {
                // Defensive fallback for non-pre-propagated data: use the
                // nearest ancestor carrying a transferable RID.
                owner = path
                    .Where(value =>
                        value.Snapshot.DepthFromItem > 0
                        && value.Snapshot.HasChildren
                        && IsTransferableRid(
                            ReadProperty(
                                value.Item,
                                RidCategoryName,
                                RidPropertyName)))
                    .OrderBy(value => value.Snapshot.DepthFromItem)
                    .FirstOrDefault();
            }

            if (owner == null)
            {
                return;
            }

            snapshot.HasRidOwner = true;
            snapshot.RidOwnerDepthFromAnchor =
                owner.Snapshot.DepthFromItem;
            snapshot.RidOwner = CloneNode(owner.Snapshot);

            int maximumDepth = Math.Min(
                path.Count - 1,
                snapshot.RidOwnerDepthFromAnchor +
                ContextNodesAboveOwner);

            foreach (PathCapture value in path)
            {
                if (value.Snapshot.DepthFromItem > maximumDepth)
                {
                    break;
                }

                snapshot.TopologyPath.Add(
                    CloneNode(value.Snapshot));
            }
        }

        private static List<PathCapture> BuildAncestorPath(ModelItem item)
        {
            var result = new List<PathCapture>();
            ModelItem current = item;
            int depth = 0;

            while (current != null)
            {
                result.Add(
                    new PathCapture(
                        current,
                        new ExternalTopologyPathNodeSnapshot
                        {
                            DepthFromItem = depth,
                            InstanceGuid = current.InstanceGuid.ToString("D"),
                            DisplayName = current.DisplayName,
                            ItemGuid = ReadProperty(current, "Item", "GUID"),
                            SourceFile = ReadSourceFile(current),
                            HasChildren = HasChildren(current)
                        }));

                current = current.Parent;
                depth++;
            }

            return result;
        }

        private static ExternalTopologyPathNodeSnapshot CloneNode(
            ExternalTopologyPathNodeSnapshot value)
        {
            if (value == null)
            {
                return null;
            }

            return new ExternalTopologyPathNodeSnapshot
            {
                DepthFromItem = value.DepthFromItem,
                InstanceGuid = value.InstanceGuid,
                DisplayName = value.DisplayName,
                ItemGuid = value.ItemGuid,
                SourceFile = value.SourceFile,
                HasChildren = value.HasChildren
            };
        }

        private static bool RidEquals(string first, string second)
        {
            if (!IsTransferableRid(first) || !IsTransferableRid(second))
            {
                return false;
            }

            return string.Equals(
                first.Trim(),
                second.Trim(),
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTransferableRid(string value)
        {
            return !string.IsNullOrWhiteSpace(value)
                && !string.Equals(
                    value.Trim(),
                    "\"\"",
                    StringComparison.Ordinal);
        }

        private static bool HasChildren(ModelItem item)
        {
            if (item == null)
            {
                return false;
            }

            foreach (ModelItem child in item.Children)
            {
                return true;
            }

            return false;
        }

        private sealed class PathCapture
        {
            public PathCapture(
                ModelItem item,
                ExternalTopologyPathNodeSnapshot snapshot)
            {
                Item = item;
                Snapshot = snapshot;
            }

            public ModelItem Item { get; private set; }

            public ExternalTopologyPathNodeSnapshot Snapshot
            {
                get;
                private set;
            }
        }

        private sealed class ValidationCompletedException : Exception
        {
        }

        private static bool ShouldPublishItemProgress(int current, int total)
        {
            if (current <= 1 || current >= total)
            {
                return true;
            }

            return total <= 100
                ? current % 5 == 0
                : current % 25 == 0;
        }

        private static void PublishProgress(
            string path,
            string stage,
            int current,
            int total,
            string message,
            bool determinate)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string recordPath = CreateProgressRecordPath(path);

            try
            {
                var value = new ExternalSourceProgress
                {
                    Stage = stage,
                    Current = current,
                    Total = total,
                    Message = message,
                    IsDeterminate = determinate
                };

                using (var stream = new FileStream(
                    recordPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.Read))
                {
                    new DataContractJsonSerializer(
                        typeof(ExternalSourceProgress))
                        .WriteObject(stream, value);
                }
            }
            catch
            {
                try
                {
                    if (File.Exists(recordPath))
                    {
                        File.Delete(recordPath);
                    }
                }
                catch
                {
                }
            }
        }

        private static string CreateProgressRecordPath(string progressPath)
        {
            return progressPath
                + "."
                + DateTime.UtcNow.Ticks.ToString("D19")
                + "."
                + Guid.NewGuid().ToString("N")
                + ".record";
        }

        private static SelectionSet FindExportSet(
            Document document,
            string name)
        {
            SavedItemCollection children =
                document.SelectionSets.RootItem.Children;

            foreach (string part in new[]
            {
                "MAE-4D",
                "DATA-TRANSFER",
                "EXPORT"
            })
            {
                List<SavedItem> matches = children
                    .Cast<SavedItem>()
                    .Where(value => string.Equals(
                        value.DisplayName,
                        part,
                        StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (matches.Count != 1 || !(matches[0] is GroupItem))
                {
                    throw new InvalidOperationException(
                        "Required folder missing or ambiguous: " + part);
                }

                children = ((GroupItem)matches[0]).Children;
            }

            List<SelectionSet> sets = children
                .Cast<SavedItem>()
                .OfType<SelectionSet>()
                .Where(value => string.Equals(
                    value.DisplayName,
                    name,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (sets.Count != 1)
            {
                throw new InvalidOperationException(
                    "Export set missing or ambiguous: " + name);
            }

            return sets[0];
        }

        private static string ReadSourceFile(ModelItem item)
        {
            string value = ReadProperty(
                item,
                "Item",
                "Source File Name");

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
            {
                return string.Empty;
            }

            foreach (PropertyCategory category in item.PropertyCategories)
            {
                if (category == null
                    || !string.Equals(
                        category.DisplayName,
                        categoryName,
                        StringComparison.OrdinalIgnoreCase))
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

                    try
                    {
                        return property.Value == null
                            ? string.Empty
                            : property.Value.ToDisplayString();
                    }
                    catch
                    {
                        return string.Empty;
                    }
                }
            }

            return string.Empty;
        }

        private static string SerializeVariantData(VariantData value)
        {
            if (value == null)
            {
                return null;
            }

            try
            {
                if (value.IsDisplayString)
                {
                    return value.ToDisplayString();
                }
            }
            catch (Exception exception)
            {
                return "<unreadable display value: "
                    + exception.GetType().Name
                    + ">";
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
                return "<unreadable value: "
                    + exception.GetType().Name
                    + ">";
            }
        }

        private static string Hash(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (var stream = new FileStream(
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
