using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows.Threading;
using Autodesk.Navisworks.Api;
using NavisLegacyPlugin.Models.Execution;
using NavisLegacyPlugin.Models.ExternalSources;
using NavisLegacyPlugin.Models.Topology;
using NavisLegacyPlugin.Services.Collections;
using NavisLegacyPlugin.Services.Matching;

namespace NavisLegacyPlugin.Services.Topology
{
    public sealed class ExternalTopologyOutcome
    {
        public ExternalTopologyOutcome(
            int rowIndex,
            TransferMatchResult match,
            TopologyResolution resolution,
            bool conflict,
            IEnumerable<TransferOperationResult> operations)
        {
            RowIndex = rowIndex;
            Match = match;
            Resolution = resolution;
            Conflict = conflict;

            Operations = (
                operations
                ?? Enumerable.Empty<TransferOperationResult>())
                .ToList()
                .AsReadOnly();
        }

        public int RowIndex
        {
            get;
            private set;
        }

        public TransferMatchResult Match
        {
            get;
            private set;
        }

        public TopologyResolution Resolution
        {
            get;
            private set;
        }

        public bool Conflict
        {
            get;
            private set;
        }

        public IReadOnlyList<TransferOperationResult> Operations
        {
            get;
            private set;
        }
    }

    internal sealed class TargetSnapshot
    {
        public ModelItem Item
        {
            get;
            set;
        }

        public string Key
        {
            get;
            set;
        }

        public TopologyItemFingerprint Fingerprint
        {
            get;
            set;
        }
    }

    internal sealed class AnchorRow
    {
        public int RowIndex
        {
            get;
            set;
        }

        public ExternalModelItemSnapshot Snapshot
        {
            get;
            set;
        }

        public string Rid
        {
            get;
            set;
        }

        public MatchResolution Match
        {
            get;
            set;
        }

        public TransferMatchResult TransferMatch
        {
            get;
            set;
        }

        public List<TopologyPathNode> SourcePath
        {
            get;
            set;
        }

        public string RouteSignature
        {
            get;
            set;
        }
    }

    public sealed class ExternalTopologyTransferCoordinator
    {
        private const string RidPropertyName = "MAE-4D.RID";

        private const string RepresentativeSelectionRule =
            "Stable row order with evenly distributed representatives; " +
            "dissent always retained.";

        private readonly ComPropertyWriteService _writer;
        private readonly TopologyAlignmentSettings _settings;
        private readonly TopologyPathBuilder _paths;

        public ExternalTopologyTransferCoordinator(
            ComPropertyWriteService writer)
        {
            if (writer == null)
            {
                throw new ArgumentNullException("writer");
            }

            _writer = writer;
            _settings = new TopologyAlignmentSettings();
            _settings.Validate();
            _paths = new TopologyPathBuilder();
        }

        public IReadOnlyList<ExternalTopologyOutcome> Execute(
            ExternalSourcePopulation population,
            DataTable table,
            IEnumerable<ModelItem> targetItems,
            MatchStrategyDefinition strategy,
            bool overwrite,
            IProgress<string> progressText = null,
            IProgress<int> progressPercent = null)
        {
            if (population == null)
            {
                throw new ArgumentNullException("population");
            }

            if (table == null)
            {
                throw new ArgumentNullException("table");
            }

            if (strategy == null)
            {
                throw new ArgumentNullException("strategy");
            }

            Stopwatch stopwatch = Stopwatch.StartNew();

            List<ModelItem> targets = (
                targetItems
                ?? Enumerable.Empty<ModelItem>())
                .Where(item => item != null)
                .Distinct(ModelItemReferenceComparer.Instance)
                .ToList();

            Log(
                "TARGET population received",
                stopwatch,
                "items=" + targets.Count);

            progressText?.Report(
                "Preparing TARGET identity cache...");

            Dictionary<string, List<TargetSnapshot>> targetIndex =
                BuildTargetIndex(
                    targets,
                    strategy,
                    progressText,
                    progressPercent);

            Log(
                "TARGET anchor index complete",
                stopwatch,
                "keys=" + targetIndex.Count);

            List<AnchorRow> anchorRows =
                ResolveSourceAnchors(
                    population,
                    table,
                    strategy,
                    targetIndex);

            Log(
                "SOURCE anchors resolved",
                stopwatch,
                "anchors=" + anchorRows.Count);

            var analyses =
                new List<TopologyGroupAnalysis>();

            var rowByAnalysis =
                new Dictionary<TopologyGroupAnalysis, int>();

            var resultRows =
                new Dictionary<int, ExternalTopologyOutcome>();

            List<IGrouping<string, AnchorRow>> validGroups =
                anchorRows
                    .Where(IsTransferableMatchedAnchor)
                    .GroupBy(
                        row =>
                            OwnerKey(row)
                            + "|"
                            + row.RouteSignature,
                        StringComparer.Ordinal)
                    .ToList();

            int groupNumber = 0;

            foreach (IGrouping<string, AnchorRow> group
                in validGroups)
            {
                groupNumber++;

                ProcessValidRouteGroup(
                    group,
                    targets,
                    analyses,
                    rowByAnalysis,
                    resultRows);

                YieldToDispatcher(
                    groupNumber,
                    validGroups.Count,
                    progressText,
                    "Analysing topology route groups",
                    progressPercent,
                    55,
                    25);
            }

            AddUnresolvedAnchorOutcomes(
                anchorRows,
                resultRows);

            progressText?.Report(
                "Reconciling topology mappings...");

            TopologyReconciliationResult reconciliation =
                new TopologyConflictReconciler()
                    .Reconcile(analyses);

            IReadOnlyList<TopologyWriteExecution> writes =
                new TopologyBranchWriteExecutor(_writer)
                    .Execute(
                        reconciliation,
                        targets,
                        overwrite);

            AddAnalysedOutcomes(
                anchorRows,
                analyses,
                rowByAnalysis,
                reconciliation,
                writes,
                resultRows);

            Log(
                "Topology outcome finalisation complete",
                stopwatch,
                "outcomes=" + resultRows.Count);

            return anchorRows
                .OrderBy(row => row.RowIndex)
                .Select(row => resultRows[row.RowIndex])
                .ToList()
                .AsReadOnly();
        }

        private Dictionary<string, List<TargetSnapshot>>
            BuildTargetIndex(
                IReadOnlyList<ModelItem> targets,
                MatchStrategyDefinition strategy,
                IProgress<string> progressText,
                IProgress<int> progressPercent)
        {
            var index =
                new Dictionary<string, List<TargetSnapshot>>(
                    StringComparer.Ordinal);

            for (int targetIndex = 0;
                targetIndex < targets.Count;
                targetIndex++)
            {
                ModelItem target = targets[targetIndex];

                string key = MatchKey.Create(
                    strategy.TargetKeySelector(target));

                var snapshot = new TargetSnapshot
                {
                    Item = target,
                    Key = key,
                    Fingerprint = _paths.CreateFingerprint(target)
                };

                if (key != null)
                {
                    List<TargetSnapshot> matchingSnapshots;

                    if (!index.TryGetValue(
                        key,
                        out matchingSnapshots))
                    {
                        matchingSnapshots =
                            new List<TargetSnapshot>();

                        index.Add(
                            key,
                            matchingSnapshots);
                    }

                    matchingSnapshots.Add(snapshot);
                }

                YieldToDispatcher(
                    targetIndex + 1,
                    targets.Count,
                    progressText,
                    "Preparing TARGET identity cache",
                    progressPercent,
                    35,
                    15);
            }

            return index;
        }

        private List<AnchorRow> ResolveSourceAnchors(
            ExternalSourcePopulation population,
            DataTable table,
            MatchStrategyDefinition strategy,
            IDictionary<string, List<TargetSnapshot>> targetIndex)
        {
            var rows = new List<AnchorRow>();

            foreach (DataRow row in table.Rows)
            {
                int sourceRowIndex =
                    Convert.ToInt32(
                        row["__SourceRowIndex"],
                        CultureInfo.InvariantCulture);

                ExternalModelItemSnapshot sourceSnapshot =
                    population.Items[sourceRowIndex];

                string key = MatchKey.Create(
                    strategy.SourceKeySelector(row));

                List<TargetSnapshot> targetMatches = null;

                if (key != null)
                {
                    targetIndex.TryGetValue(
                        key,
                        out targetMatches);
                }

                MatchResolution match =
                    CreateMatchResolution(
                        strategy,
                        key,
                        targetMatches);

                List<TopologyPathNode> sourcePath =
                    ToPath(sourceSnapshot);

                rows.Add(
                    new AnchorRow
                    {
                        RowIndex = sourceRowIndex,
                        Snapshot = sourceSnapshot,
                        Rid = ReadColumn(
                            row,
                            RidPropertyName),
                        Match = match,
                        TransferMatch = ToTransferMatch(
                            match,
                            targetMatches),
                        SourcePath = sourcePath,
                        RouteSignature = BuildRouteSignature(
                            sourcePath)
                    });
            }

            return rows;
        }

        private static MatchResolution CreateMatchResolution(
            MatchStrategyDefinition strategy,
            string key,
            IReadOnlyCollection<TargetSnapshot> targetMatches)
        {
            if (key == null)
            {
                return new MatchResolution(
                    MatchResolutionStatus.MissingKey,
                    null,
                    strategy.Name,
                    null,
                    "SOURCE anchor key is incomplete.");
            }

            if (targetMatches == null
                || targetMatches.Count == 0)
            {
                return new MatchResolution(
                    MatchResolutionStatus.Unmatched,
                    null,
                    strategy.Name,
                    key,
                    "No TARGET anchor matched.");
            }

            if (targetMatches.Count > 1)
            {
                return new MatchResolution(
                    MatchResolutionStatus.Ambiguous,
                    null,
                    strategy.Name,
                    key,
                    "More than one TARGET anchor matched.");
            }

            return new MatchResolution(
                MatchResolutionStatus.Matched,
                targetMatches.First().Item,
                strategy.Name,
                key,
                "Matched unique TARGET anchor.");
        }

        private void ProcessValidRouteGroup(
            IGrouping<string, AnchorRow> group,
            IReadOnlyCollection<ModelItem> targets,
            ICollection<TopologyGroupAnalysis> analyses,
            IDictionary<TopologyGroupAnalysis, int> rowByAnalysis,
            IDictionary<int, ExternalTopologyOutcome> resultRows)
        {
            List<AnchorRow> groupRows =
                group
                    .OrderBy(row => row.RowIndex)
                    .ToList();

            List<IGrouping<ModelItem, AnchorRow>>
                rowsByTargetAnchor =
                    groupRows
                        .GroupBy(
                            row => row.Match.Item,
                            ModelItemReferenceComparer.Instance)
                        .OrderByDescending(
                            targetGroup => targetGroup.Count())
                        .ToList();

            IGrouping<ModelItem, AnchorRow> majorityGroup =
                rowsByTargetAnchor.First();

            List<AnchorRow> agreeingRows =
                majorityGroup.ToList();

            List<AnchorRow> dissentingRows =
                groupRows
                    .Except(agreeingRows)
                    .ToList();

            List<AnchorRow> representativeRows =
                SelectRepresentatives(
                    agreeingRows,
                    _settings
                        .MaximumRepresentativeAnchorsPerRoute);

            List<AnchorRow> detailedRows =
                representativeRows
                    .Concat(dissentingRows)
                    .Distinct()
                    .ToList();

            var anchorSummary =
                new TopologyAnchorSummary(
                    groupRows.Count,
                    groupRows.Count,
                    groupRows.Count,
                    0,
                    0,
                    agreeingRows.Count,
                    dissentingRows.Count,
                    1,
                    representativeRows.Count,
                    _settings
                        .MaximumRepresentativeAnchorsPerRoute,
                    RepresentativeSelectionRule);

            var groupAnalyses =
                new List<TopologyGroupAnalysis>();

            foreach (AnchorRow anchorRow in detailedRows)
            {
                TopologyGroupAnalysis analysis =
                    AnalyseAnchorRow(
                        anchorRow,
                        targets,
                        anchorSummary,
                        dissentingRows.Count > 0);

                analyses.Add(analysis);
                groupAnalyses.Add(analysis);
                rowByAnalysis[analysis] =
                    anchorRow.RowIndex;
            }

            AddSaturatedOutcomes(
                agreeingRows,
                representativeRows,
                groupAnalyses,
                resultRows);
        }

        private TopologyGroupAnalysis AnalyseAnchorRow(
            AnchorRow anchorRow,
            IReadOnlyCollection<ModelItem> targets,
            TopologyAnchorSummary anchorSummary,
            bool hasDissent)
        {
            TopologyItemFingerprint sourceOwner =
                OwnerFingerprint(anchorRow);

            var anchorEvidence =
                new[]
                {
                    CreateAnchorEvidence(anchorRow)
                };

            ModelItem candidateOwner =
                MapOwner(
                    anchorRow.Match.Item,
                    anchorRow.SourcePath);

            var candidates =
                new List<TopologyTargetCandidate>();

            if (candidateOwner != null
                && targets.Contains(
                    candidateOwner,
                    ModelItemReferenceComparer.Instance))
            {
                candidates.Add(
                    new TopologyTargetCandidate(
                        candidateOwner,
                        _paths.CreateFingerprint(candidateOwner),
                        _paths.BuildFromAnchorToAncestors(
                            anchorRow.Match.Item,
                            Math.Max(
                                1,
                                anchorRow.SourcePath.Count))));
            }

            var sourceGroup =
                new TopologySourceGroup(
                    anchorRow.RowIndex.ToString(
                        CultureInfo.InvariantCulture),
                    anchorRow.Rid,
                    sourceOwner,
                    anchorRow.SourcePath,
                    anchorEvidence);

            TopologyGroupAnalysis initialAnalysis =
                new TopologyAnalysisService(_settings)
                    .Analyse(
                        sourceGroup,
                        candidates);

            TopologyResolution initialResolution =
                initialAnalysis.Resolution;

            var resolution =
                new TopologyResolution(
                    initialResolution.Status,
                    initialResolution.Reason,
                    initialResolution.WriteEligible,
                    initialResolution.BestScore,
                    initialResolution.NextBestScore,
                    initialResolution.Anchors,
                    initialResolution.SourceRidOwner,
                    initialResolution.RidValue,
                    initialResolution.SourcePath,
                    initialResolution.Candidates,
                    anchorSummary);

            TopologyTargetCandidate selectedCandidate =
                initialAnalysis.SelectedCandidate;

            if (hasDissent)
            {
                resolution =
                    new TopologyResolution(
                        TopologyResolutionStatus.Ambiguous,
                        "Definitive anchors propose different TARGET routes.",
                        false,
                        initialResolution.BestScore,
                        initialResolution.NextBestScore,
                        initialResolution.Anchors,
                        initialResolution.SourceRidOwner,
                        initialResolution.RidValue,
                        initialResolution.SourcePath,
                        initialResolution.Candidates,
                        anchorSummary);

                selectedCandidate = null;
            }

            return new TopologyGroupAnalysis(
                sourceGroup,
                resolution,
                selectedCandidate);
        }

        private static void AddSaturatedOutcomes(
            IEnumerable<AnchorRow> agreeingRows,
            IEnumerable<AnchorRow> representativeRows,
            IReadOnlyCollection<TopologyGroupAnalysis> groupAnalyses,
            IDictionary<int, ExternalTopologyOutcome> resultRows)
        {
            HashSet<AnchorRow> representatives =
                new HashSet<AnchorRow>(
                    representativeRows);

            foreach (AnchorRow saturatedRow
                in agreeingRows.Where(
                    row => !representatives.Contains(row)))
            {
                TopologyGroupAnalysis representativeAnalysis =
                    groupAnalyses.FirstOrDefault(
                        analysis =>
                            string.Equals(
                                analysis.Source.RidValue,
                                saturatedRow.Rid,
                                StringComparison.OrdinalIgnoreCase));

                if (representativeAnalysis == null)
                {
                    continue;
                }

                resultRows[saturatedRow.RowIndex] =
                    new ExternalTopologyOutcome(
                        saturatedRow.RowIndex,
                        saturatedRow.TransferMatch,
                        representativeAnalysis.Resolution,
                        false,
                        new TransferOperationResult[0]);
            }
        }

        private void AddUnresolvedAnchorOutcomes(
            IEnumerable<AnchorRow> anchorRows,
            IDictionary<int, ExternalTopologyOutcome> resultRows)
        {
            foreach (AnchorRow anchorRow
                in anchorRows.Where(
                    row => !IsTransferableMatchedAnchor(row)))
            {
                TopologyAnchorEvidence[] evidence =
                {
                    CreateAnchorEvidence(anchorRow)
                };

                TopologyResolutionStatus status =
                    anchorRow.Match.Status
                        == MatchResolutionStatus.Ambiguous
                        ? TopologyResolutionStatus.Ambiguous
                        : TopologyResolutionStatus.Unresolved;

                var anchorSummary =
                    new TopologyAnchorSummary(
                        1,
                        anchorRow.Match.Status
                            == MatchResolutionStatus.MissingKey
                            ? 0
                            : 1,
                        anchorRow.Match.Status
                            == MatchResolutionStatus.Matched
                            ? 1
                            : 0,
                        anchorRow.Match.Status
                            == MatchResolutionStatus.Unmatched
                            ? 1
                            : 0,
                        anchorRow.Match.Status
                            == MatchResolutionStatus.Ambiguous
                            ? 1
                            : 0,
                        0,
                        0,
                        1,
                        0,
                        _settings
                            .MaximumRepresentativeAnchorsPerRoute,
                        "No representative alignment for unresolved anchor.");

                var resolution =
                    new TopologyResolution(
                        status,
                        anchorRow.Match.Reason,
                        false,
                        0,
                        0,
                        evidence,
                        OwnerFingerprint(anchorRow),
                        anchorRow.Rid,
                        anchorRow.SourcePath,
                        new TopologyCandidateEvidence[0],
                        anchorSummary);

                resultRows[anchorRow.RowIndex] =
                    new ExternalTopologyOutcome(
                        anchorRow.RowIndex,
                        anchorRow.TransferMatch,
                        resolution,
                        false,
                        new TransferOperationResult[0]);
            }
        }

        private static void AddAnalysedOutcomes(
            IReadOnlyCollection<AnchorRow> anchorRows,
            IEnumerable<TopologyGroupAnalysis> analyses,
            IDictionary<TopologyGroupAnalysis, int> rowByAnalysis,
            TopologyReconciliationResult reconciliation,
            IEnumerable<TopologyWriteExecution> writes,
            IDictionary<int, ExternalTopologyOutcome> resultRows)
        {
            List<TopologyWriteExecution> writeResults =
                (
                    writes
                    ?? Enumerable.Empty<TopologyWriteExecution>())
                    .ToList();

            foreach (TopologyGroupAnalysis analysis in analyses)
            {
                int rowIndex =
                    rowByAnalysis[analysis];

                bool conflict =
                    reconciliation.Blocked.Contains(analysis)
                    && analysis.Resolution.Status
                        == TopologyResolutionStatus.Good
                    && analysis.SelectedCandidate != null;

                var operations =
                    new List<TransferOperationResult>();

                TopologyWritePlan plan =
                    reconciliation.Plans.FirstOrDefault(
                        currentPlan =>
                            currentPlan.Sources.Contains(analysis));

                TopologyWriteExecution write =
                    plan == null
                        ? null
                        : writeResults.FirstOrDefault(
                            result => result.Plan == plan);

                if (write != null)
                {
                    operations.Add(
                        new TransferOperationResult(
                            write.Status,
                            "PropertyWrite",
                            "MAE-4D",
                            "RID",
                            analysis.Source.RidValue,
                            write.Reason,
                            write.Error));
                }

                AnchorRow sourceRow =
                    anchorRows.First(
                        row => row.RowIndex == rowIndex);

                resultRows[rowIndex] =
                    new ExternalTopologyOutcome(
                        rowIndex,
                        sourceRow.TransferMatch,
                        analysis.Resolution,
                        conflict,
                        operations);
            }
        }

        private static bool IsTransferableMatchedAnchor(
            AnchorRow row)
        {
            if (row == null
                || row.Match == null
                || row.Match.Status
                    != MatchResolutionStatus.Matched)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(row.Rid))
            {
                return false;
            }

            return !string.Equals(
                row.Rid.Trim(),
                "\"\"",
                StringComparison.Ordinal);
        }

        private static List<AnchorRow> SelectRepresentatives(
            List<AnchorRow> availableRows,
            int maximumRepresentatives)
        {
            if (availableRows == null)
            {
                return new List<AnchorRow>();
            }

            if (availableRows.Count <= maximumRepresentatives)
            {
                return new List<AnchorRow>(availableRows);
            }

            var representatives =
                new List<AnchorRow>();

            if (maximumRepresentatives == 1)
            {
                representatives.Add(availableRows[0]);
                return representatives;
            }

            for (int index = 0;
                index < maximumRepresentatives;
                index++)
            {
                int sourceIndex =
                    (int)Math.Round(
                        index
                        * (availableRows.Count - 1d)
                        / (maximumRepresentatives - 1d));

                AnchorRow representative =
                    availableRows[sourceIndex];

                if (!representatives.Contains(representative))
                {
                    representatives.Add(representative);
                }
            }

            return representatives;
        }

        private static ModelItem MapOwner(
            ModelItem targetAnchor,
            IReadOnlyList<TopologyPathNode> sourcePath)
        {
            if (targetAnchor == null)
            {
                return null;
            }

            int sourcePathCount =
                sourcePath == null
                    ? 0
                    : sourcePath.Count;

            int ownerDepth =
                Math.Max(
                    0,
                    sourcePathCount - 1);

            ModelItem current = targetAnchor;

            for (int depth = 0;
                depth < ownerDepth && current != null;
                depth++)
            {
                current = current.Parent;
            }

            return current;
        }

        private static string BuildRouteSignature(
            IEnumerable<TopologyPathNode> path)
        {
            return string.Join(
                ">",
                (
                    path
                    ?? Enumerable.Empty<TopologyPathNode>())
                    .Select(
                        node =>
                            (node.DisplayName ?? string.Empty)
                                .Trim()
                                .ToUpperInvariant())
                    .ToArray());
        }

        private static string OwnerKey(
            AnchorRow anchorRow)
        {
            TopologyItemFingerprint owner =
                OwnerFingerprint(anchorRow);

            return (
                owner.InstanceGuid
                + "|"
                + owner.ItemGuid
                + "|"
                + owner.SourceFile
                + "|"
                + owner.DisplayName)
                .ToUpperInvariant();
        }

        private static TopologyItemFingerprint OwnerFingerprint(
            AnchorRow anchorRow)
        {
            ExternalTopologyPathNodeSnapshot ownerNode =
                anchorRow.Snapshot.AncestorPath == null
                || anchorRow.Snapshot.AncestorPath.Count == 0
                    ? null
                    : anchorRow.Snapshot.AncestorPath[
                        anchorRow.Snapshot.AncestorPath.Count - 1];

            if (ownerNode == null)
            {
                return CreateSourceFingerprint(
                    anchorRow.Snapshot);
            }

            return new TopologyItemFingerprint(
                ownerNode.InstanceGuid,
                ownerNode.ItemGuid,
                ownerNode.SourceFile,
                ownerNode.DisplayName);
        }

        private TopologyAnchorEvidence CreateAnchorEvidence(
            AnchorRow anchorRow)
        {
            return new TopologyAnchorEvidence(
                anchorRow.Match.StrategyName,
                anchorRow.Match.Key,
                ToTopologyAnchorStatus(
                    anchorRow.Match.Status),
                anchorRow.Match.Reason,
                CreateSourceFingerprint(
                    anchorRow.Snapshot),
                anchorRow.Match.Item == null
                    ? null
                    : _paths.CreateFingerprint(
                        anchorRow.Match.Item));
        }

        private static List<TopologyPathNode> ToPath(
            ExternalModelItemSnapshot snapshot)
        {
            return (
                snapshot.AncestorPath
                ?? new List<ExternalTopologyPathNodeSnapshot>())
                .Select(
                    node =>
                        new TopologyPathNode(
                            node.DepthFromItem,
                            node.DisplayName,
                            new TopologyItemFingerprint(
                                node.InstanceGuid,
                                node.ItemGuid,
                                node.SourceFile,
                                node.DisplayName)))
                .ToList();
        }

        private static void YieldToDispatcher(
            int current,
            int total,
            IProgress<string> progressText,
            string label,
            IProgress<int> progressPercent,
            int progressStart,
            int progressSpan)
        {
            bool shouldReport =
                current == 1
                || current == total
                || current % 25 == 0;

            if (!shouldReport)
            {
                return;
            }

            progressText?.Report(
                label
                + " "
                + current
                + "/"
                + total
                + "...");

            int progressValue =
                progressStart
                + (
                    total == 0
                        ? 0
                        : current * progressSpan / total);

            progressPercent?.Report(progressValue);

            if (System.Windows.Application.Current != null)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(
                    () => { },
                    DispatcherPriority.Background);
            }
        }

        private static void Log(
            string stage,
            Stopwatch stopwatch,
            string data)
        {
            Logger.Info(
                "[C-C1 R03] "
                + stage
                + "; "
                + data
                + "; elapsedMs="
                + stopwatch.ElapsedMilliseconds
                + ".");
        }

        private static string ReadColumn(
            DataRow row,
            string columnName)
        {
            if (row == null
                || row.Table == null
                || !row.Table.Columns.Contains(columnName)
                || row[columnName] == DBNull.Value)
            {
                return string.Empty;
            }

            return Convert.ToString(
                       row[columnName],
                       CultureInfo.InvariantCulture)
                   ?? string.Empty;
        }

        private static TopologyItemFingerprint
            CreateSourceFingerprint(
                ExternalModelItemSnapshot snapshot)
        {
            string itemGuid =
                GetSnapshotProperty(
                    snapshot,
                    "Item",
                    "GUID");

            string sourceFile =
                GetSnapshotProperty(
                    snapshot,
                    "Item",
                    "Source File Name");

            if (string.IsNullOrWhiteSpace(sourceFile))
            {
                sourceFile =
                    GetSnapshotProperty(
                        snapshot,
                        "Item",
                        "Source File");
            }

            return new TopologyItemFingerprint(
                snapshot.InstanceGuid,
                itemGuid,
                sourceFile,
                snapshot.DisplayName);
        }

        private static string GetSnapshotProperty(
            ExternalModelItemSnapshot snapshot,
            string categoryName,
            string propertyName)
        {
            ExternalPropertySnapshot property =
                (
                    snapshot.Properties
                    ?? new List<ExternalPropertySnapshot>())
                    .FirstOrDefault(
                        candidate =>
                            candidate != null
                            && string.Equals(
                                candidate.Category,
                                categoryName,
                                StringComparison.OrdinalIgnoreCase)
                            && string.Equals(
                                candidate.Name,
                                propertyName,
                                StringComparison.OrdinalIgnoreCase));

            return property == null
                ? string.Empty
                : property.Value ?? string.Empty;
        }

        private static TopologyAnchorStatus
            ToTopologyAnchorStatus(
                MatchResolutionStatus status)
        {
            switch (status)
            {
                case MatchResolutionStatus.Matched:
                    return TopologyAnchorStatus.Matched;

                case MatchResolutionStatus.Ambiguous:
                    return TopologyAnchorStatus.Ambiguous;

                case MatchResolutionStatus.MissingKey:
                    return TopologyAnchorStatus.MissingKey;

                default:
                    return TopologyAnchorStatus.Unmatched;
            }
        }

        private static TransferMatchResult ToTransferMatch(
            MatchResolution match,
            IReadOnlyCollection<TargetSnapshot> targetMatches)
        {
            TransferMatchStatus status;

            switch (match.Status)
            {
                case MatchResolutionStatus.Matched:
                    status = TransferMatchStatus.Matched;
                    break;

                case MatchResolutionStatus.Ambiguous:
                    status = TransferMatchStatus.Ambiguous;
                    break;

                case MatchResolutionStatus.MissingKey:
                    status = TransferMatchStatus.MissingKey;
                    break;

                default:
                    status = TransferMatchStatus.Unmatched;
                    break;
            }

            TopologyItemFingerprint fingerprint =
                targetMatches != null
                && targetMatches.Count == 1
                    ? targetMatches.First().Fingerprint
                    : null;

            TransferTargetFingerprint transferFingerprint =
                fingerprint == null
                    ? null
                    : new TransferTargetFingerprint(
                        fingerprint.InstanceGuid,
                        fingerprint.ItemGuid,
                        fingerprint.SourceFile,
                        fingerprint.DisplayName);

            return new TransferMatchResult(
                status,
                match.StrategyName,
                match.Key,
                match.Reason,
                transferFingerprint);
        }
    }
}