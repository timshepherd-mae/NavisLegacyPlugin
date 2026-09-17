using Autodesk.Navisworks.Api;
using System;
using System.Collections.Generic;

namespace NavisLegacyPlugin.Services.SelectionSets
{
    public sealed class DataTransferHierarchyValidator
        : IDataTransferHierarchyValidator
    {
        private readonly INavisSelectionSetProvider
            _selectionSetProvider;

        public DataTransferHierarchyValidator(
            INavisSelectionSetProvider selectionSetProvider)
        {
            if (selectionSetProvider == null)
            {
                throw new ArgumentNullException(
                    "selectionSetProvider");
            }

            _selectionSetProvider =
                selectionSetProvider;
        }

        public DataTransferHierarchyValidationResult Validate(
            Document document)
        {
            var result =
                new DataTransferHierarchyValidationResult();

            if (document == null)
            {
                result.AddError(
                    DataTransferHierarchyErrorCode
                        .Mae4dRootMissing,
                    "No active Navisworks document is available.",
                    DataTransferSelectionSetNames.Root);

                return result;
            }

            GroupItem dataTransferGroup =
                ValidateContainers(
                    document,
                    result);

            if (dataTransferGroup == null)
            {
                return result;
            }

            ValidateScopeUnderGroup(
                document,
                dataTransferGroup,
                DataTransferSelectionSetNames.Source,
                false,
                result);

            ValidateScopeUnderGroup(
                document,
                dataTransferGroup,
                DataTransferSelectionSetNames.Target,
                false,
                result);

            ValidateScopeUnderGroup(
                document,
                dataTransferGroup,
                DataTransferSelectionSetNames.Export,
                false,
                result);

            return result;
        }

        public DataTransferHierarchyValidationResult ValidateScope(
            Document document,
            string scopeName)
        {
            var result =
                new DataTransferHierarchyValidationResult();

            if (document == null)
            {
                result.AddError(
                    DataTransferHierarchyErrorCode
                        .Mae4dRootMissing,
                    "No active Navisworks document is available.",
                    DataTransferSelectionSetNames.Root);

                return result;
            }

            if (!IsReservedScopeName(scopeName))
            {
                throw new ArgumentException(
                    "The requested scope must be SOURCE, TARGET or EXPORT.",
                    "scopeName");
            }

            GroupItem dataTransferGroup =
                ValidateContainers(
                    document,
                    result);

            if (dataTransferGroup == null)
            {
                return result;
            }

            ValidateScopeUnderGroup(
                document,
                dataTransferGroup,
                scopeName,
                true,
                result);

            return result;
        }

        private GroupItem ValidateContainers(
            Document document,
            DataTransferHierarchyValidationResult result)
        {
            IReadOnlyList<SavedItem> rootMatches =
                _selectionSetProvider.FindRootItems(
                    document,
                    DataTransferSelectionSetNames.Root);

            if (rootMatches.Count == 0)
            {
                result.AddError(
                    DataTransferHierarchyErrorCode
                        .Mae4dRootMissing,
                    "The MAE-4D Selection Set folder is missing.",
                    DataTransferSelectionSetNames.Root);

                return null;
            }

            if (rootMatches.Count > 1)
            {
                result.AddError(
                    DataTransferHierarchyErrorCode
                        .Mae4dRootDuplicate,
                    "More than one MAE-4D item exists at the Selection Set root.",
                    DataTransferSelectionSetNames.Root);

                return null;
            }

            GroupItem mae4dGroup =
                rootMatches[0] as GroupItem;

            if (mae4dGroup == null)
            {
                result.AddError(
                    DataTransferHierarchyErrorCode
                        .Mae4dRootMissing,
                    "MAE-4D exists but is not a Selection Set folder.",
                    DataTransferSelectionSetNames.Root);

                return null;
            }

            IReadOnlyList<SavedItem> dataTransferMatches =
                _selectionSetProvider.FindChildren(
                    mae4dGroup,
                    DataTransferSelectionSetNames.DataTransfer);

            string dataTransferPath =
                BuildPath(
                    DataTransferSelectionSetNames.Root,
                    DataTransferSelectionSetNames.DataTransfer);

            if (dataTransferMatches.Count == 0)
            {
                result.AddError(
                    DataTransferHierarchyErrorCode
                        .DataTransferMissing,
                    "The DATA-TRANSFER Selection Set folder is missing.",
                    dataTransferPath);

                return null;
            }

            if (dataTransferMatches.Count > 1)
            {
                result.AddError(
                    DataTransferHierarchyErrorCode
                        .DataTransferDuplicate,
                    "More than one DATA-TRANSFER item exists beneath MAE-4D.",
                    dataTransferPath);

                return null;
            }

            GroupItem dataTransferGroup =
                dataTransferMatches[0] as GroupItem;

            if (dataTransferGroup == null)
            {
                result.AddError(
                    DataTransferHierarchyErrorCode
                        .DataTransferMissing,
                    "DATA-TRANSFER exists but is not a Selection Set folder.",
                    dataTransferPath);

                return null;
            }

            return dataTransferGroup;
        }

        private void ValidateScopeUnderGroup(
    Document document,
    GroupItem dataTransferGroup,
    string scopeName,
    bool requireNonEmpty,
    DataTransferHierarchyValidationResult result)
        {
            IReadOnlyList<SavedItem> matches =
                _selectionSetProvider.FindChildren(
                    dataTransferGroup,
                    scopeName);

            string scopePath =
                BuildPath(
                    DataTransferSelectionSetNames.Root,
                    DataTransferSelectionSetNames.DataTransfer,
                    scopeName);

            if (matches.Count == 0)
            {
                result.AddError(
                    GetMissingCode(scopeName),
                    string.Format(
                        "The {0} Selection Set is missing.",
                        scopeName),
                    scopePath);

                return;
            }

            if (matches.Count > 1)
            {
                result.AddError(
                    GetDuplicateCode(scopeName),
                    string.Format(
                        "More than one {0} item exists beneath DATA-TRANSFER.",
                        scopeName),
                    scopePath);

                return;
            }

            SelectionSet selectionSet =
                matches[0] as SelectionSet;

            if (selectionSet == null)
            {
                result.AddError(
                    GetInvalidTypeCode(scopeName),
                    string.Format(
                        "{0} exists but is not a Selection Set.",
                        scopeName),
                    scopePath);

                return;
            }

            if (!requireNonEmpty)
            {
                return;
            }

            IReadOnlyList<ModelItem> items =
                _selectionSetProvider.GetSelectedItems(
                    document,
                    selectionSet);

            if (items.Count == 0)
            {
                result.AddError(
                    GetEmptyCode(scopeName),
                    string.Format(
                        "The {0} Selection Set contains no model items.",
                        scopeName),
                    scopePath);
            }
        }

        private static bool IsReservedScopeName(
            string scopeName)
        {
            return
                string.Equals(
                    scopeName,
                    DataTransferSelectionSetNames.Source,
                    StringComparison.OrdinalIgnoreCase)
                ||
                string.Equals(
                    scopeName,
                    DataTransferSelectionSetNames.Target,
                    StringComparison.OrdinalIgnoreCase)
                ||
                string.Equals(
                    scopeName,
                    DataTransferSelectionSetNames.Export,
                    StringComparison.OrdinalIgnoreCase);
        }

        private static string GetMissingCode(
            string scopeName)
        {
            if (string.Equals(
                scopeName,
                DataTransferSelectionSetNames.Source,
                StringComparison.OrdinalIgnoreCase))
            {
                return DataTransferHierarchyErrorCode
                    .SourceScopeMissing;
            }

            if (string.Equals(
                scopeName,
                DataTransferSelectionSetNames.Target,
                StringComparison.OrdinalIgnoreCase))
            {
                return DataTransferHierarchyErrorCode
                    .TargetScopeMissing;
            }

            return DataTransferHierarchyErrorCode
                .ExportScopeMissing;
        }

        private static string GetDuplicateCode(
            string scopeName)
        {
            if (string.Equals(
                scopeName,
                DataTransferSelectionSetNames.Source,
                StringComparison.OrdinalIgnoreCase))
            {
                return DataTransferHierarchyErrorCode
                    .SourceScopeDuplicate;
            }

            if (string.Equals(
                scopeName,
                DataTransferSelectionSetNames.Target,
                StringComparison.OrdinalIgnoreCase))
            {
                return DataTransferHierarchyErrorCode
                    .TargetScopeDuplicate;
            }

            return DataTransferHierarchyErrorCode
                .ExportScopeDuplicate;
        }

        private static string GetInvalidTypeCode(
            string scopeName)
        {
            if (string.Equals(
                scopeName,
                DataTransferSelectionSetNames.Source,
                StringComparison.OrdinalIgnoreCase))
            {
                return DataTransferHierarchyErrorCode
                    .SourceScopeInvalidType;
            }

            if (string.Equals(
                scopeName,
                DataTransferSelectionSetNames.Target,
                StringComparison.OrdinalIgnoreCase))
            {
                return DataTransferHierarchyErrorCode
                    .TargetScopeInvalidType;
            }

            return DataTransferHierarchyErrorCode
                .ExportScopeInvalidType;
        }

        private static string GetEmptyCode(
            string scopeName)
        {
            if (string.Equals(
                scopeName,
                DataTransferSelectionSetNames.Source,
                StringComparison.OrdinalIgnoreCase))
            {
                return DataTransferHierarchyErrorCode
                    .SourceScopeEmpty;
            }

            if (string.Equals(
                scopeName,
                DataTransferSelectionSetNames.Target,
                StringComparison.OrdinalIgnoreCase))
            {
                return DataTransferHierarchyErrorCode
                    .TargetScopeEmpty;
            }

            return DataTransferHierarchyErrorCode
                .ExportScopeEmpty;
        }

        private static string BuildPath(
            params string[] parts)
        {
            return string.Join(
                "/",
                parts);
        }
    }
}
