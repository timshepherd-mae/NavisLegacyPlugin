using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Autodesk.Navisworks.Api;
using NavisLegacyPlugin.Models;
using NavisLegacyPlugin.Models.Execution;
using NavisLegacyPlugin.Models.ExternalSources;
using NavisLegacyPlugin.Models.Scopes;
using NavisLegacyPlugin.Services.Collections;
using NavisLegacyPlugin.Services.Matching;
using NavisLegacyPlugin.Services.SelectionSets;

namespace NavisLegacyPlugin.Services.Execution
{
    public sealed class ExecuteSignatureExecutor
    {
        private readonly ComPropertyWriteService _writer;
        private readonly IDataTransferHierarchyValidator _hierarchyValidator;
        private readonly ITransferScopeResolver _scopeResolver;
        private readonly ITransferScopeCollectionResolver _scopeCollectionResolver;

        public ExecuteSignatureExecutor(
            ComPropertyWriteService writer)
            : this(
                writer,
                new DataTransferHierarchyValidator(
                    new NavisSelectionSetProvider()),
                new NavisScopeResolver(),
                new TransferScopeCollectionResolver())
        {
        }

        public ExecuteSignatureExecutor(
            ComPropertyWriteService writer,
            IDataTransferHierarchyValidator hierarchyValidator,
            ITransferScopeResolver scopeResolver,
            ITransferScopeCollectionResolver scopeCollectionResolver)
        {
            _writer = writer
                ?? throw new ArgumentNullException("writer");

            _hierarchyValidator = hierarchyValidator
                ?? throw new ArgumentNullException("hierarchyValidator");

            _scopeResolver = scopeResolver
                ?? throw new ArgumentNullException("scopeResolver");

            _scopeCollectionResolver = scopeCollectionResolver
                ?? throw new ArgumentNullException("scopeCollectionResolver");
        }

        public Task<ExecuteResult> ExecuteAsync(
            ExecuteSignature signature)
        {
            return ExecuteLegacyAsync(signature);
        }

        public Task<ExecuteResult> ExecuteAsync(
            ExecuteSignature signature,
            ExternalSourcePopulation external)
        {
            return ExecuteAsync(
                signature,
                external,
                null);
        }

        public async Task<ExecuteResult> ExecuteAsync(
            ExecuteSignature signature,
            ExternalSourcePopulation external,
            IRowMatchResolver resolver)
        {
            if (external == null || resolver == null)
            {
                return await ExecuteLegacyAsync(signature);
            }

            return await ExecuteExternalDetailedAsync(
                signature,
                external,
                resolver);
        }

        private async Task<ExecuteResult> ExecuteExternalDetailedAsync(
            ExecuteSignature signature,
            ExternalSourcePopulation population,
            IRowMatchResolver resolver)
        {
            if (signature == null)
            {
                throw new ArgumentNullException("signature");
            }

            DataTable table =
                await signature.DataSource.GetDataAsync(
                    signature.ProgressConfig.ProgressText);

            var builders =
                new Dictionary<int, OutcomeBuilder>();

            for (int i = 0; i < population.Items.Count; i++)
            {
                builders[i] =
                    new OutcomeBuilder(
                        i,
                        population.Items[i]);
            }

            var writeMap =
                new Dictionary<
                    ModelItem,
                    Dictionary<
                        string,
                        Dictionary<string, PendingWrite>>>();

            int processed = 0;

            signature.ProgressConfig.ProgressText?
                .Report("Grouping data...");

            foreach (DataRow row in table.Rows)
            {
                processed++;

                // Phase 7.3 SOURCE correlation.
                //
                // __SourceRowIndex is populated by
                // ExternalSourcePopulationAdapter from the ordered
                // ExternalModelItemSnapshot collection.
                //
                // Defensive fallback:
                // if the reserved column is absent or contains DBNull,
                // use the zero-based DataTable row ordinal rather than
                // attempting to convert DBNull to Int32.
                int sourceRowIndex = processed - 1;

                if (row.Table.Columns.Contains("__SourceRowIndex"))
                {
                    object sourceRowIndexValue =
                        row["__SourceRowIndex"];

                    if (sourceRowIndexValue != null &&
                        sourceRowIndexValue != DBNull.Value)
                    {
                        sourceRowIndex =
                            Convert.ToInt32(
                                sourceRowIndexValue);
                    }
                }

                OutcomeBuilder builder;

                if (!builders.TryGetValue(
                    sourceRowIndex,
                    out builder))
                {
                    continue;
                }

                var instruction =
                    signature.MappingStrategy.Map(row);

                if (instruction == null)
                {
                    builder.Match =
                        new TransferMatchResult(
                            TransferMatchStatus.MissingKey,
                            string.Empty,
                            string.Empty,
                            "Mapping did not produce an executable instruction.",
                            null);

                    continue;
                }

                MatchResolution match =
                    resolver.Resolve(row);
                if (match.Status == MatchResolutionStatus.Matched &&
                    match.Item != null &&
                    !resolver.IsTargetMember(match.Item))
                {
                    match = new MatchResolution(
                        MatchResolutionStatus.Unmatched,
                        null,
                        match.StrategyName,
                        match.Key,
                        "Matched item is outside the resolved TARGET population.");
                }
                builder.Match =
                    ToTransferMatchResult(match);
                if (match.Status !=
                        MatchResolutionStatus.Matched ||
                    match.Item == null)
                {
                    continue;
                }

                foreach (var tab
                    in instruction.PropertiesByTab)
                {
                    Dictionary<
                        string,
                        Dictionary<string, PendingWrite>>
                        tabs;

                    if (!writeMap.TryGetValue(
                        match.Item,
                        out tabs))
                    {
                        tabs =
                            new Dictionary<
                                string,
                                Dictionary<string, PendingWrite>>(
                                    StringComparer.OrdinalIgnoreCase);

                        writeMap[match.Item] = tabs;
                    }

                    Dictionary<string, PendingWrite>
                        properties;

                    if (!tabs.TryGetValue(
                        tab.Key,
                        out properties))
                    {
                        properties =
                            new Dictionary<
                                string,
                                PendingWrite>(
                                    StringComparer.OrdinalIgnoreCase);

                        tabs[tab.Key] = properties;
                    }

                    foreach (var property
                        in tab.Value)
                    {
                                                // Approved Phase 7.3 defect correction:
                        // SOURCE rows without a transferable RID may still match a TARGET,
                        // but they must not create an MAE-4D/RID write operation.  Leaving
                        // the builder with no applicable operation causes the retained
                        // SOURCE-level operation status to remain NotApplicable.
                        if (IsNonTransferableExternalRid(tab.Key, property.Key, property.Value))
                        {
                            continue;
                        }
                        PendingWrite pendingWrite;

                        if (!properties.TryGetValue(
                            property.Key,
                            out pendingWrite))
                        {
                            pendingWrite =
                                new PendingWrite(
                                    tab.Key,
                                    property.Key,
                                    property.Value);

                            properties[property.Key] =
                                pendingWrite;
                        }

                        pendingWrite.Builders.Add(
                            builder);
                    }
                }

                if (processed % 50 == 0)
                {
                    int percent =
                        35 +
                        (
                            processed * 30 /
                            Math.Max(
                                1,
                                table.Rows.Count)
                        );

                    signature.ProgressConfig
                        .ProgressPercent?
                        .Report(percent);

                    signature.ProgressConfig
                        .ProgressText?
                        .Report(
                            "Grouping " +
                            processed +
                            "/" +
                            table.Rows.Count);

                    await System.Windows.Application
                        .Current
                        .Dispatcher
                        .InvokeAsync(
                            () => { },
                            System.Windows.Threading
                                .DispatcherPriority
                                .Background);
                }
            }

            int writeIndex = 0;

            foreach (var targetEntry in writeMap)
            {
                writeIndex++;

                foreach (var tabEntry
                    in targetEntry.Value)
                {
                    foreach (PendingWrite pendingWrite
                        in tabEntry.Value.Values)
                    {
                        var category =
                            targetEntry.Key
                                .PropertyCategories
                                .FindCategoryByDisplayName(
                                    pendingWrite.Tab);

                        var existingProperty =
                            category?
                                .Properties
                                .FindPropertyByDisplayName(
                                    pendingWrite.Property);

                        if (!signature.WriteConfig.Overwrite &&
                            existingProperty != null)
                        {
                            pendingWrite.AddResult(
                                TransferOperationStatus.Skipped,
                                "Existing value preserved.",
                                string.Empty);

                            continue;
                        }

                        try
                        {
                            _writer.WriteUserDefinedProperties(
                                targetEntry.Key,
                                pendingWrite.Tab,
                                new Dictionary<string, string>
                                {
                                    {
                                        pendingWrite.Property,
                                        pendingWrite.Value
                                    }
                                });

                            pendingWrite.AddResult(
                                TransferOperationStatus.Written,
                                "Property written.",
                                string.Empty);
                        }
                        catch (Exception exception)
                        {
                            // Phase 7.3:
                            // retain this operation failure but allow
                            // unrelated property operations to continue.
                            pendingWrite.AddResult(
                                TransferOperationStatus.Failed,
                                "Property write failed.",
                                exception.Message);
                        }
                    }
                }

                if (writeIndex % 10 == 0)
                {
                    int percent =
                        65 +
                        (
                            writeIndex * 35 /
                            Math.Max(
                                1,
                                writeMap.Count)
                        );

                    signature.ProgressConfig
                        .ProgressPercent?
                        .Report(percent);

                    signature.ProgressConfig
                        .ProgressText?
                        .Report(
                            "Writing " +
                            writeIndex +
                            "/" +
                            writeMap.Count);

                    await System.Windows.Application
                        .Current
                        .Dispatcher
                        .InvokeAsync(
                            () => { },
                            System.Windows.Threading
                                .DispatcherPriority
                                .Background);
                }
            }

            List<TransferOutcome> outcomes =
                builders
                    .OrderBy(pair => pair.Key)
                    .Select(
                        pair =>
                            pair.Value.CreateOutcome())
                    .ToList();

            TransferExecutionStatus executionStatus =
                outcomes.Any(
                    outcome =>
                        outcome.OperationStatus ==
                        TransferOperationStatus.Failed)
                    ? TransferExecutionStatus
                        .CompletedWithFailures
                    : TransferExecutionStatus
                        .Completed;

            var outcomeSet =
                new TransferOutcomeSet(
                    Guid.NewGuid().ToString("D"),
                    population.SourceFile,
                    population.ExportSetName,
                    population.ResolutionType,
                    signature.SelectionSets?
                        .TargetResolutionType
                        .ToString(),
                    executionStatus,
                    outcomes);

            signature.ProgressConfig
                .ProgressPercent?
                .Report(100);

            signature.ProgressConfig
                .ProgressText?
                .Report("Complete.");

            return new ExecuteResult(outcomeSet);
        }

        private async Task<ExecuteResult>
            ExecuteLegacyAsync(
                ExecuteSignature signature)
        {
            if (signature == null)
            {
                throw new ArgumentNullException(
                    "signature");
            }

            var lookup =
                await signature.LookupProvider
                    .BuildLookupAsync(
                        signature.ProgressConfig);

            DataTable table =
                await signature.DataSource
                    .GetDataAsync(
                        signature.ProgressConfig
                            .ProgressText);

            int matched = 0;
            int unmatched = 0;
            int written = 0;
            int skipped = 0;

            foreach (DataRow row in table.Rows)
            {
                var instruction =
                    signature.MappingStrategy.Map(row);

                if (instruction == null ||
                    string.IsNullOrWhiteSpace(
                        instruction.MatchValue))
                {
                    unmatched++;
                    continue;
                }

                ModelItem item;

                if (!lookup.TryGetValue(
                    instruction.MatchValue,
                    out item))
                {
                    unmatched++;
                    continue;
                }

                matched++;

                foreach (var tab
                    in instruction.PropertiesByTab)
                {
                    foreach (var property
                        in tab.Value)
                    {
                        var category =
                            item.PropertyCategories
                                .FindCategoryByDisplayName(
                                    tab.Key);

                        var existingProperty =
                            category?
                                .Properties
                                .FindPropertyByDisplayName(
                                    property.Key);

                        if (!signature.WriteConfig.Overwrite &&
                            existingProperty != null)
                        {
                            skipped++;
                            continue;
                        }

                        _writer.WriteUserDefinedProperties(
                            item,
                            tab.Key,
                            new Dictionary<string, string>
                            {
                                {
                                    property.Key,
                                    property.Value
                                }
                            });

                        written++;
                    }
                }
            }

            return new ExecuteResult(
                matched,
                unmatched,
                written,
                skipped);
        }

        private static bool IsNonTransferableExternalRid(
            string tabName,
            string propertyName,
            string value)
        {
            if (!string.Equals(
                    tabName,
                    "MAE-4D",
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(
                    propertyName,
                    "RID",
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Keep this eligibility rule local to the external RID transfer path.
            // Other generic property-writing workflows are still allowed to write
            // an empty string where that behaviour is intentional.
            return string.IsNullOrWhiteSpace(value) ||
                string.Equals(value.Trim(), "\"\"", StringComparison.Ordinal);
        }
        private static TransferMatchResult
            ToTransferMatchResult(
                MatchResolution match)
        {
            TransferMatchStatus status;

            switch (match.Status)
            {
                case MatchResolutionStatus.Matched:
                    status =
                        TransferMatchStatus.Matched;
                    break;

                case MatchResolutionStatus.Ambiguous:
                    status =
                        TransferMatchStatus.Ambiguous;
                    break;

                case MatchResolutionStatus.MissingKey:
                    status =
                        TransferMatchStatus.MissingKey;
                    break;

                default:
                    status =
                        TransferMatchStatus.Unmatched;
                    break;
            }

            TransferTargetFingerprint fingerprint =
                null;

            if (match.Item != null)
            {
                fingerprint =
                    new TransferTargetFingerprint(
                        match.Item.InstanceGuid
                            .ToString("D"),
                        ReadProperty(
                            match.Item,
                            "Item",
                            "GUID"),
                        ReadSourceFile(
                            match.Item),
                        match.Item.DisplayName);
            }

            return new TransferMatchResult(
                status,
                match.StrategyName,
                match.Key,
                match.Reason,
                fingerprint);
        }

        private static string ReadProperty(
            ModelItem item,
            string categoryName,
            string propertyName)
        {
            var category =
                item.PropertyCategories
                    .FindCategoryByDisplayName(
                        categoryName);

            var property =
                category?
                    .Properties
                    .FindPropertyByDisplayName(
                        propertyName);

            return property?
                .Value?
                .ToDisplayString()
                ?? string.Empty;
        }

        private static string ReadSourceFile(
            ModelItem item)
        {
            string value =
                ReadProperty(
                    item,
                    "Item",
                    "Source File Name");

            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            return ReadProperty(
                item,
                "Item",
                "Source File");
        }

        private sealed class OutcomeBuilder
        {
            private readonly
                ExternalModelItemSnapshot _snapshot;

            public OutcomeBuilder(
                int index,
                ExternalModelItemSnapshot snapshot)
            {
                Index = index;
                _snapshot = snapshot;
                Operations =
                    new List<
                        TransferOperationResult>();
            }

            public int Index { get; private set; }

            public TransferMatchResult Match
            {
                get;
                set;
            }

            public List<TransferOperationResult>
                Operations
            {
                get;
                private set;
            }

            public TransferOutcome CreateOutcome()
            {
                List<TransferPayloadValue> payload =
                    (_snapshot?.Properties ??
                        new List<
                            ExternalPropertySnapshot>())
                    .Where(
                        property =>
                            property != null)
                    .Select(
                        property =>
                            new TransferPayloadValue(
                                (property.Category ??
                                    string.Empty) +
                                "." +
                                (property.Name ??
                                    string.Empty),
                                property.Value))
                    .ToList();

                string itemGuid =
                    GetSnapshotProperty(
                        "Item",
                        "GUID");

                string sourceFile =
                    GetSnapshotProperty(
                        "Item",
                        "Source File Name");

                if (string.IsNullOrWhiteSpace(
                    sourceFile))
                {
                    sourceFile =
                        GetSnapshotProperty(
                            "Item",
                            "Source File");
                }

                var sourceIdentity =
                    new TransferSourceIdentity(
                        Index,
                        _snapshot?.InstanceGuid,
                        itemGuid,
                        sourceFile,
                        _snapshot?.DisplayName);

                TransferMatchResult finalMatch =
                    Match ??
                    new TransferMatchResult(
                        TransferMatchStatus
                            .MissingKey,
                        string.Empty,
                        string.Empty,
                        "No executable SOURCE row was produced.",
                        null);

                return new TransferOutcome(
                    sourceIdentity,
                    payload,
                    finalMatch,
                    Operations);
            }

            private string GetSnapshotProperty(
                string categoryName,
                string propertyName)
            {
                ExternalPropertySnapshot property =
                    _snapshot?
                        .Properties?
                        .FirstOrDefault(
                            value =>
                                value != null &&
                                string.Equals(
                                    value.Category,
                                    categoryName,
                                    StringComparison
                                        .OrdinalIgnoreCase) &&
                                string.Equals(
                                    value.Name,
                                    propertyName,
                                    StringComparison
                                        .OrdinalIgnoreCase));

                return property?.Value
                    ?? string.Empty;
            }
        }

        private sealed class PendingWrite
        {
            public PendingWrite(
                string tab,
                string property,
                string value)
            {
                Tab = tab;
                Property = property;
                Value = value;

                Builders =
                    new List<OutcomeBuilder>();
            }

            public string Tab
            {
                get;
                private set;
            }

            public string Property
            {
                get;
                private set;
            }

            public string Value
            {
                get;
                private set;
            }

            public List<OutcomeBuilder> Builders
            {
                get;
                private set;
            }

            public void AddResult(
                TransferOperationStatus status,
                string reason,
                string error)
            {
                foreach (OutcomeBuilder builder
                    in Builders)
                {
                    builder.Operations.Add(
                        new TransferOperationResult(
                            status,
                            "PropertyWrite",
                            Tab,
                            Property,
                            Value,
                            reason,
                            error));
                }
            }
        }
    }
}
