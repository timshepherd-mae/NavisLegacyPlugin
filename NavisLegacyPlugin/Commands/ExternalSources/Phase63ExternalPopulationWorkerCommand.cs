using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Plugins;
using NavisLegacyPlugin.Models.Collections;
using NavisLegacyPlugin.Models.ExternalSources;
using NavisLegacyPlugin.Services.Collections;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;

namespace NavisLegacyPlugin.Commands.ExternalSources
{
    [Plugin("Phase63ExternalPopulationWorker", "MAE", DisplayName = "Phase 6.3 External Population Worker")]
    public sealed class Phase63ExternalPopulationWorkerCommand : AddInPlugin
    {
        public override int Execute(params string[] parameters)
        {
            if (parameters == null || parameters.Length < 5 || parameters[0] != "WORKER") return -1;

            string output = parameters[1];
            string source = parameters[2];
            string exportName = parameters[3];
            string resolutionName = parameters[4];

            ExternalExportPopulationResponse response = new ExternalExportPopulationResponse
            {
                SourceFile = source,
                ExportSetName = exportName,
                ResolutionType = resolutionName
            };

            try
            {
                response.SourceHashBefore = Hash(source);

                Document document = Autodesk.Navisworks.Api.Application.ActiveDocument;
                if (document == null)
                {
                    throw new InvalidOperationException("Worker ActiveDocument is null.");
                }

                SelectionSet set = FindExportSet(document, exportName);
                ModelItemCollection selected = set.GetSelectedItems();

                ModelItemCollectionResolver resolver = new ModelItemCollectionResolver();
                CollectionResolutionResult resolved = resolver.Resolve(selected);

                response.AllCount = resolved.AllItems.Count;
                response.BranchCount = resolved.BranchItems.Count;
                response.LeafCount = resolved.LeafItems.Count;

                CollectionResolutionType type;
                if (!Enum.TryParse(resolutionName, true, out type))
                {
                    throw new InvalidOperationException("Invalid resolution type: " + resolutionName);
                }

                IEnumerable<ModelItem> outputItems =
                    type == CollectionResolutionType.Branch
                        ? resolved.BranchItems
                        : type == CollectionResolutionType.Leaf
                            ? resolved.LeafItems
                            : resolved.AllItems;

                foreach (ModelItem item in outputItems)
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

            using (FileStream stream = File.Create(output))
            {
                DataContractJsonSerializer serializer =
                    new DataContractJsonSerializer(typeof(ExternalExportPopulationResponse));
                serializer.WriteObject(stream, response);
            }

            return response.Success ? 0 : -1;
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
                {
                    throw new InvalidOperationException(
                        "Required folder missing or ambiguous: " + part);
                }

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
            {
                throw new InvalidOperationException(
                    "Export set missing or ambiguous: " + name);
            }

            return sets[0];
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
