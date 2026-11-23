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

        public string AnchorDisplayName
        {
            get;
            set;
        }

        public string ImmediateChildBranch
        {
            get;
            set;
        }
    }

    public sealed class ExternalTopologyTransferCoordinator
    {
        private const string RidPropertyName = "MAE-4D.RID";

        private const string RepresentativeSelectionRule =
            "Distinct immediate child branches, then distinct DisplayNames, " +
            "then distinct anchor keys, then evenly distributed SOURCE row order; " +
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

            List<ModelItem> targetAnchors = targets
                .Where(item => !HasChildren(item))
                .ToList();

            List<ModelItem> targetOwners = targets
                .Where(HasChildren)
                .ToList();

            var targetOwnerBoundary = new HashSet<ModelItem>(
                targetOwners,
                ModelItemReferenceComparer.Instance);

            Dictionary<string, List<TargetSnapshot>> targetIndex =
                BuildTargetIndex(
                    targetAnchors,
                    strategy,
                    progressText,
                    progressPercent);

            Log(
                "TARGET anchor index complete",
                stopwatch,
                "keys=" + targetIndex.Count
                + "; targetLeaves=" + targetAnchors.Count
                + "; targetBranches=" + targetOwners.Count);

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

            Log(
                "SOURCE topology applicability assessed",
                stopwatch,
                "counts={" + BuildApplicabilitySummary(anchorRows) + "}");

            LogStructuralBranchEvidence(
                anchorRows,
                stopwatch);

            var analyses =
                new List<TopologyGroupAnalysis>();

            var rowByAnalysis =
                new Dictionary<TopologyGroupAnalysis, int>();

            var resultRows =
                new Dictionary<int, ExternalTopologyOutcome>();

            List<IGrouping<string, AnchorRow>> validGroups =
                anchorRows
                    .Where(IsTopologyApplicableMatchedAnchor)
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
                    targetOwnerBoundary,
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

            AddNotApplicableOutcomes(
                anchorRows,
                resultRows);

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
                "sourceRows=" + anchorRows.Count
                + "; notApplicable=" + anchorRows.Count(row => !IsTopologyApplicable(row))
                + "; branchOwnerGroups=" + analyses.Count
                + "; outcomes=" + resultRows.Count
                + "; leafWriteAttempts=0"
                + "; applicability={" + BuildApplicabilitySummary(anchorRows) + "}");

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
                            sourceSnapshot),
                        AnchorDisplayName = sourceSnapshot == null
                            ? string.Empty
                            : sourceSnapshot.DisplayName ?? string.Empty,
                        ImmediateChildBranch = GetImmediateChildBranchName(
                            sourceSnapshot)
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
            ISet<ModelItem> targetOwnerBoundary,
            ICollection<TopologyGroupAnalysis> analyses,
            IDictionary<TopologyGroupAnalysis, int> rowByAnalysis,
            IDictionary<int, ExternalTopologyOutcome> resultRows)
        {
            List<AnchorRow> groupRows = group
                .OrderBy(row => row.RowIndex)
                .ToList();

            List<IGrouping<ModelItem, AnchorRow>> rowsByMappedOwner = groupRows
                .GroupBy(
                    row => MapOwner(row.Match.Item, row.Snapshot),
                    ModelItemReferenceComparer.Instance)
                .Where(mappedGroup =>
                    mappedGroup.Key != null
                    && HasChildren(mappedGroup.Key)
                    && targetOwnerBoundary.Contains(mappedGroup.Key))
                .OrderByDescending(mappedGroup => mappedGroup.Count())
                .ToList();

            if (rowsByMappedOwner.Count == 0)
            {
                AnchorRow decisionRow = groupRows.First();
                resultRows[decisionRow.RowIndex] = BuildNoMappedOwnerOutcome(decisionRow);

                foreach (AnchorRow evidenceRow in groupRows.Skip(1))
                {
                    resultRows[evidenceRow.RowIndex] =
                        BuildSupportingEvidenceOutcome(
                            evidenceRow,
                            "Supporting leaf evidence; no in-boundary TARGET branch owner was produced for the owner group.");
                }

                LogSaturation(groupRows, 0, 0, 0, 0,
                    "No in-boundary TARGET branch owner was produced for the owner group.");
                return;
            }

            IGrouping<ModelItem, AnchorRow> majorityGroup = rowsByMappedOwner.First();
            List<AnchorRow> agreeingRows = majorityGroup
                .OrderBy(row => row.RowIndex)
                .ToList();
            List<AnchorRow> dissentingRows = groupRows
                .Where(row => !agreeingRows.Contains(row))
                .OrderBy(row => row.RowIndex)
                .ToList();

            List<AnchorRow> representativeRows = SelectRepresentatives(
                agreeingRows,
                _settings.MaximumRepresentativeAnchorsPerRoute);
            List<AnchorRow> detailedRows = representativeRows
                .Concat(dissentingRows)
                .Distinct()
                .OrderBy(row => row.RowIndex)
                .ToList();

            var anchorSummary = new TopologyAnchorSummary(
                groupRows.Count,
                groupRows.Count,
                groupRows.Count,
                0,
                0,
                agreeingRows.Count,
                dissentingRows.Count,
                1,
                representativeRows.Count,
                _settings.MaximumRepresentativeAnchorsPerRoute,
                RepresentativeSelectionRule);

            var detailedAnalyses = new List<TopologyGroupAnalysis>();
            var detailedRowsByAnalysis = new Dictionary<TopologyGroupAnalysis, AnchorRow>();

            foreach (AnchorRow anchorRow in detailedRows)
            {
                TopologyGroupAnalysis analysis = AnalyseAnchorRow(
                    anchorRow,
                    targets,
                    targetOwnerBoundary,
                    anchorSummary,
                    dissentingRows.Count > 0);
                detailedAnalyses.Add(analysis);
                detailedRowsByAnalysis[analysis] = anchorRow;
            }

            TopologyGroupAnalysis canonicalAnalysis = detailedAnalyses
                .OrderByDescending(value => value.Resolution.BestScore)
                .ThenBy(value => detailedRowsByAnalysis[value].RowIndex)
                .First();
            AnchorRow canonicalRow = detailedRowsByAnalysis[canonicalAnalysis];

            // One analysis, reconciliation source and write plan per distinct
            // branch-owner/RID/route group. All other leaves remain evidence.
            analyses.Add(canonicalAnalysis);
            rowByAnalysis[canonicalAnalysis] = canonicalRow.RowIndex;

            foreach (AnchorRow evidenceRow in groupRows)
            {
                if (evidenceRow.RowIndex == canonicalRow.RowIndex)
                {
                    continue;
                }

                resultRows[evidenceRow.RowIndex] = BuildSupportingEvidenceOutcome(
                    evidenceRow,
                    "Supporting leaf anchor evidence for branch-owner group "
                    + canonicalAnalysis.Source.GroupId
                    + "; the owner-group decision is retained on SOURCE row "
                    + canonicalRow.RowIndex.ToString(CultureInfo.InvariantCulture)
                    + ".");
            }

            LogSaturation(
                groupRows,
                agreeingRows.Count,
                dissentingRows.Count,
                representativeRows.Count,
                detailedRows.Count,
                "One canonical branch-owner analysis retained for reconciliation and mutation.");
        }

        private ExternalTopologyOutcome BuildSupportingEvidenceOutcome(
            AnchorRow anchorRow,
            string reason)
        {
            return new ExternalTopologyOutcome(
                anchorRow.RowIndex,
                anchorRow.TransferMatch,
                null,
                false,
                new[]
                {
                    new TransferOperationResult(
                        TransferOperationStatus.NotApplicable,
                        "TopologyAnchorEvidence",
                        "MAE-4D",
                        "RID",
                        anchorRow.Rid,
                        reason,
                        string.Empty)
                });
        }

        private ExternalTopologyOutcome BuildNoMappedOwnerOutcome(
            AnchorRow anchorRow)
        {
            var summary = new TopologyAnchorSummary(
                1,
                1,
                1,
                0,
                0,
                0,
                1,
                1,
                0,
                _settings.MaximumRepresentativeAnchorsPerRoute,
                "No representative selected because no mapped TARGET owner exists.");

            var resolution = new TopologyResolution(
                TopologyResolutionStatus.Unresolved,
                "The matched TARGET anchor did not produce an in-boundary owner route.",
                false,
                0,
                0,
                new[] { CreateAnchorEvidence(anchorRow) },
                OwnerFingerprint(anchorRow),
                anchorRow.Rid,
                anchorRow.SourcePath,
                new TopologyCandidateEvidence[0],
                summary);

            return new ExternalTopologyOutcome(
                anchorRow.RowIndex,
                anchorRow.TransferMatch,
                resolution,
                false,
                new TransferOperationResult[0]);
        }

        private TopologyGroupAnalysis AnalyseAnchorRow(
            AnchorRow anchorRow,
            IReadOnlyCollection<ModelItem> targets,
            ISet<ModelItem> targetOwnerBoundary,
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
                    anchorRow.Snapshot);

            List<ModelItem> candidateItems =
                BuildCandidateNeighbourhood(
                    anchorRow.Match.Item,
                    anchorRow.Snapshot.RidOwnerDepthFromAnchor);

            var candidates =
                new List<TopologyTargetCandidate>();
            int rejectedOutsideBoundary = 0;
            int maximumNodes = Math.Max(
                1,
                anchorRow.SourcePath.Count + 1);

            foreach (ModelItem candidateItem in candidateItems)
            {
                if (!HasChildren(candidateItem)
                    || !targetOwnerBoundary.Contains(candidateItem)
                    || !targets.Contains(
                        candidateItem,
                        ModelItemReferenceComparer.Instance))
                {
                    rejectedOutsideBoundary++;
                    continue;
                }

                candidates.Add(
                    new TopologyTargetCandidate(
                        candidateItem,
                        _paths.CreateFingerprint(candidateItem),
                        _paths.BuildFromAnchorToAncestors(
                            anchorRow.Match.Item,
                            maximumNodes)));
            }

            bool referenceContains = candidateOwner != null
                && targets.Contains(
                    candidateOwner,
                    ModelItemReferenceComparer.Instance);

            int fingerprintMatches = candidateOwner == null
                ? 0
                : targets.Count(target => FingerprintsEqual(
                    _paths.CreateFingerprint(target),
                    _paths.CreateFingerprint(candidateOwner)));

            LogCandidateMapping(
                anchorRow,
                sourceOwner,
                candidateOwner,
                referenceContains,
                fingerprintMatches,
                candidates.Count,
                rejectedOutsideBoundary,
                candidateItems.Count);
            var sourceGroup =
                new TopologySourceGroup(
                    anchorRow.RowIndex.ToString(
                        CultureInfo.InvariantCulture),
                    anchorRow.Rid,
                    sourceOwner,
                    anchorRow.SourcePath,
                    anchorEvidence,
                    anchorRow.Snapshot.RidOwnerDepthFromAnchor);

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
                    row => IsTopologyApplicable(row) && !IsTopologyApplicableMatchedAnchor(row)))
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

        private static bool IsTopologyApplicableMatchedAnchor(
            AnchorRow row)
        {
            return IsTopologyApplicable(row)
                && row.Match != null
                && row.Match.Status == MatchResolutionStatus.Matched;
        }

        private static bool IsTopologyApplicable(AnchorRow row)
        {
            return AssessTopologyApplicability(row).IsApplicable;
        }

        private static TopologyApplicabilityAssessment AssessTopologyApplicability(
            AnchorRow row)
        {
            if (row == null || row.Snapshot == null)
            {
                return new TopologyApplicabilityAssessment(
                    false,
                    TopologyApplicabilityReasonCode.OwnerNotResolved,
                    "The SOURCE topology row or snapshot is missing.");
            }

            string anchorKey = row.Match == null
                ? string.Empty
                : row.Match.Key;
            bool ownerHasChildren = row.Snapshot.RidOwner != null
                && row.Snapshot.RidOwner.HasChildren;

            return TopologyApplicabilityRules.Assess(
                row.Snapshot.HasChildren,
                row.Snapshot.HasRidOwner,
                row.Snapshot.RidOwnerDepthFromAnchor,
                ownerHasChildren,
                row.Rid,
                anchorKey);
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

        private static void AddNotApplicableOutcomes(
            IEnumerable<AnchorRow> anchorRows,
            IDictionary<int, ExternalTopologyOutcome> resultRows)
        {
            foreach (AnchorRow row in anchorRows.Where(value => !IsTopologyApplicable(value)))
            {
                resultRows[row.RowIndex] = new ExternalTopologyOutcome(
                    row.RowIndex,
                    row.TransferMatch,
                    null,
                    false,
                    new[]
                    {
                        new TransferOperationResult(
                            TransferOperationStatus.NotApplicable,
                            "TopologyEligibility",
                            "MAE-4D",
                            "RID",
                            row.Rid,
                            BuildTopologyEligibilityReason(row),
                            string.Empty)
                    });
            }
        }


        private static string BuildTopologyEligibilityReason(AnchorRow row)
        {
            TopologyApplicabilityAssessment assessment =
                AssessTopologyApplicability(row);
            string workerStatus = row == null || row.Snapshot == null
                ? "None"
                : row.Snapshot.RidOwnerEvidenceStatus.ToString();
            string workerReason = row == null || row.Snapshot == null
                ? string.Empty
                : row.Snapshot.RidOwnerEvidenceReason ?? string.Empty;
            string recoveredOwner = row == null
                || row.Snapshot == null
                || row.Snapshot.RecoveredRidOwner == null
                    ? "<none>"
                    : row.Snapshot.RecoveredRidOwner.DisplayName ?? string.Empty;
            int recoveredDepth = row == null || row.Snapshot == null
                ? -1
                : row.Snapshot.RecoveredRidOwnerDepthFromAnchor;

            return "[" + assessment.ReasonCode + "] "
                + assessment.Reason
                + " WorkerStatus=" + workerStatus
                + "; WorkerReason=" + workerReason
                + "; RecoveredOwner=" + recoveredOwner
                + "; RecoveredDepth="
                + recoveredDepth.ToString(CultureInfo.InvariantCulture);
        }

        private static string BuildApplicabilitySummary(
            IEnumerable<AnchorRow> rows)
        {
            List<IGrouping<TopologyApplicabilityReasonCode, AnchorRow>> groups =
                (rows ?? Enumerable.Empty<AnchorRow>())
                    .GroupBy(row => AssessTopologyApplicability(row).ReasonCode)
                    .OrderBy(group => group.Key.ToString())
                    .ToList();

            string applicability = string.Join(
                "; ",
                groups.Select(group => group.Key + "=" + group.Count()));
            string recovered = string.Join(
                "; ",
                (rows ?? Enumerable.Empty<AnchorRow>())
                    .GroupBy(row => row == null || row.Snapshot == null
                        ? ExternalRidOwnerEvidenceStatus.None
                        : row.Snapshot.RidOwnerEvidenceStatus)
                    .OrderBy(group => group.Key.ToString())
                    .Select(group => group.Key + "=" + group.Count()));

            return applicability + "}; recoveredOwnerStatus={" + recovered;
        }

        private static List<AnchorRow> SelectRepresentatives(
            List<AnchorRow> availableRows,
            int maximumRepresentatives)
        {
            if (availableRows == null || maximumRepresentatives <= 0)
            {
                return new List<AnchorRow>();
            }

            List<AnchorRow> ordered = availableRows
                .Where(row => row != null)
                .OrderBy(row => row.RowIndex)
                .ToList();

            if (ordered.Count <= maximumRepresentatives)
            {
                return ordered;
            }

            var selected = new List<AnchorRow>();
            var selectedSet = new HashSet<AnchorRow>();
            var childBranches = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var displayNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var anchorKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. Prefer distinct immediate child branches beneath the owner.
            AddDistinctBy(
                ordered,
                maximumRepresentatives,
                row => row.ImmediateChildBranch,
                childBranches,
                selected,
                selectedSet);

            // 2. Then prefer distinct anchor DisplayNames.
            AddDistinctBy(
                ordered,
                maximumRepresentatives,
                row => row.AnchorDisplayName,
                displayNames,
                selected,
                selectedSet);

            // 3. Then prefer distinct normalized anchor keys.
            AddDistinctBy(
                ordered,
                maximumRepresentatives,
                row => row.Match == null ? string.Empty : row.Match.Key,
                anchorKeys,
                selected,
                selectedSet);

            // 4. Fill the remaining positions deterministically using evenly
            // distributed rows from the as-yet-unselected population.
            List<AnchorRow> remaining = ordered
                .Where(row => !selectedSet.Contains(row))
                .ToList();

            int positions = maximumRepresentatives - selected.Count;
            if (positions > 0 && remaining.Count > 0)
            {
                foreach (AnchorRow row in EvenlyDistributed(
                    remaining,
                    positions))
                {
                    if (selectedSet.Add(row))
                    {
                        selected.Add(row);
                    }
                }
            }

            return selected
                .OrderBy(row => row.RowIndex)
                .Take(maximumRepresentatives)
                .ToList();
        }

        private static void AddDistinctBy(
            IEnumerable<AnchorRow> ordered,
            int maximumRepresentatives,
            Func<AnchorRow, string> selector,
            ISet<string> seenValues,
            ICollection<AnchorRow> selected,
            ISet<AnchorRow> selectedSet)
        {
            foreach (AnchorRow row in ordered)
            {
                if (selected.Count >= maximumRepresentatives)
                {
                    return;
                }

                if (selectedSet.Contains(row))
                {
                    continue;
                }

                string value = NormalizeName(selector(row));
                if (string.IsNullOrWhiteSpace(value)
                    || !seenValues.Add(value))
                {
                    continue;
                }

                selected.Add(row);
                selectedSet.Add(row);
            }
        }

        private static IEnumerable<AnchorRow> EvenlyDistributed(
            IReadOnlyList<AnchorRow> rows,
            int required)
        {
            if (rows == null || rows.Count == 0 || required <= 0)
            {
                yield break;
            }

            if (required >= rows.Count)
            {
                foreach (AnchorRow row in rows)
                {
                    yield return row;
                }
                yield break;
            }

            if (required == 1)
            {
                yield return rows[rows.Count / 2];
                yield break;
            }

            var indexes = new HashSet<int>();
            for (int index = 0; index < required; index++)
            {
                int sourceIndex = (int)Math.Round(
                    index * (rows.Count - 1d) / (required - 1d));
                indexes.Add(sourceIndex);
            }

            foreach (int index in indexes.OrderBy(value => value))
            {
                yield return rows[index];
            }
        }

        private ModelItem MapOwner(
            ModelItem targetAnchor,
            ExternalModelItemSnapshot sourceSnapshot)
        {
            if (targetAnchor == null
                || !HasValidOwnerEvidence(sourceSnapshot))
            {
                return null;
            }

            return _paths.GetAncestorAtDepth(
                targetAnchor,
                sourceSnapshot.RidOwnerDepthFromAnchor);
        }

        private List<ModelItem> BuildCandidateNeighbourhood(
            ModelItem targetAnchor,
            int expectedOwnerDepth)
        {
            var candidates = new List<ModelItem>();
            var seen = new HashSet<ModelItem>(
                ModelItemReferenceComparer.Instance);

            int[] depths =
            {
                expectedOwnerDepth,
                expectedOwnerDepth - 1,
                expectedOwnerDepth + 1
            };

            foreach (int depth in depths)
            {
                if (depth < 0)
                {
                    continue;
                }

                ModelItem item = _paths.GetAncestorAtDepth(
                    targetAnchor,
                    depth);

                if (item != null && HasChildren(item) && seen.Add(item))
                {
                    candidates.Add(item);
                }
            }

            return candidates;
        }

        private static bool HasValidOwnerEvidence(
            ExternalModelItemSnapshot snapshot)
        {
            return snapshot != null
                && snapshot.HasRidOwner
                && snapshot.RidOwner != null
                && snapshot.RidOwnerDepthFromAnchor >= 0;
        }

        private static string BuildRouteSignature(
            ExternalModelItemSnapshot snapshot)
        {
            if (!HasValidOwnerEvidence(snapshot))
            {
                return "NO-RID-OWNER";
            }

            IEnumerable<ExternalTopologyPathNodeSnapshot> route =
                (snapshot.TopologyPath
                    ?? new List<ExternalTopologyPathNodeSnapshot>())
                .Where(node =>
                    node != null
                    && node.DepthFromItem > 0
                    && node.DepthFromItem < snapshot.RidOwnerDepthFromAnchor);

            string sequence = string.Join(
                ">",
                route.Select(node =>
                    NormalizeName(node.DisplayName)
                    + (node.HasChildren ? "#B" : "#L"))
                    .ToArray());

            return snapshot.RidOwnerDepthFromAnchor
                .ToString(CultureInfo.InvariantCulture)
                + "|"
                + sequence;
        }

        private static string NormalizeName(string value)
        {
            return (value ?? string.Empty)
                .Trim()
                .ToUpperInvariant();
        }

        private static string GetImmediateChildBranchName(
            ExternalModelItemSnapshot snapshot)
        {
            if (!HasValidOwnerEvidence(snapshot)
                || snapshot.RidOwnerDepthFromAnchor <= 1)
            {
                return string.Empty;
            }

            int childDepth = snapshot.RidOwnerDepthFromAnchor - 1;
            ExternalTopologyPathNodeSnapshot child =
                (snapshot.TopologyPath
                    ?? new List<ExternalTopologyPathNodeSnapshot>())
                .FirstOrDefault(node =>
                    node != null
                    && node.DepthFromItem == childDepth);

            return child == null
                ? string.Empty
                : child.DisplayName ?? string.Empty;
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
                anchorRow == null || anchorRow.Snapshot == null
                    ? null
                    : anchorRow.Snapshot.RidOwner;

            if (ownerNode == null)
            {
                return CreateSourceFingerprint(
                    anchorRow == null ? null : anchorRow.Snapshot);
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
            IEnumerable<ExternalTopologyPathNodeSnapshot> path =
                snapshot == null
                    ? Enumerable.Empty<ExternalTopologyPathNodeSnapshot>()
                    : snapshot.TopologyPath != null
                        && snapshot.TopologyPath.Count > 0
                            ? snapshot.TopologyPath
                            : snapshot.AncestorPath
                                ?? new List<ExternalTopologyPathNodeSnapshot>();

            return path
                .Where(node => node != null)
                .OrderBy(node => node.DepthFromItem)
                .Select(node =>
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

        private static bool FingerprintsEqual(
            TopologyItemFingerprint first,
            TopologyItemFingerprint second)
        {
            if (first == null || second == null)
            {
                return false;
            }

            return string.Equals(first.InstanceGuid, second.InstanceGuid, StringComparison.OrdinalIgnoreCase)
                && string.Equals(first.ItemGuid, second.ItemGuid, StringComparison.OrdinalIgnoreCase)
                && string.Equals(first.SourceFile, second.SourceFile, StringComparison.OrdinalIgnoreCase)
                && string.Equals(first.DisplayName, second.DisplayName, StringComparison.OrdinalIgnoreCase);
        }

        private static void LogCandidateMapping(
            AnchorRow anchorRow,
            TopologyItemFingerprint sourceOwner,
            ModelItem targetOwner,
            bool referenceContains,
            int fingerprintMatches,
            int candidateCount,
            int rejectedOutsideBoundary,
            int consideredCandidateCount)
        {
            string targetFingerprint = targetOwner == null
                ? "<null>"
                : targetOwner.InstanceGuid.ToString("D")
                    + "|"
                    + (targetOwner.DisplayName ?? string.Empty);

            Logger.Info(
                "[C-C1 M05-M06] row="
                + anchorRow.RowIndex
                + "; anchorKey="
                + (anchorRow.Match.Key ?? string.Empty)
                + "; sourceOwnerDepth="
                + anchorRow.Snapshot.RidOwnerDepthFromAnchor
                    .ToString(CultureInfo.InvariantCulture)
                + "; sourceOwner="
                + (sourceOwner == null ? string.Empty : sourceOwner.DisplayName)
                + "; mappedTarget="
                + targetFingerprint
                + "; referenceContains="
                + referenceContains
                + "; fingerprintMatches="
                + fingerprintMatches
                + "; consideredCandidates="
                + consideredCandidateCount
                + "; candidateCount="
                + candidateCount
                + "; rejectedOutsideBoundary="
                + rejectedOutsideBoundary
                + ".");
        }
        private static void LogSaturation(
            IReadOnlyCollection<AnchorRow> groupRows,
            int agreeingCount,
            int dissentingCount,
            int representativeCount,
            int detailedCount,
            string reason)
        {
            AnchorRow first = groupRows == null
                ? null
                : groupRows.OrderBy(row => row.RowIndex).FirstOrDefault();

            Logger.Info(
                "[C-C1 M07] owner="
                + (first == null ? string.Empty : OwnerKey(first))
                + "; route="
                + (first == null ? string.Empty : first.RouteSignature)
                + "; totalAnchors="
                + (groupRows == null ? 0 : groupRows.Count)
                + "; agreeing="
                + agreeingCount
                + "; dissenting="
                + dissentingCount
                + "; representatives="
                + representativeCount
                + "; detailed="
                + detailedCount
                + "; reason="
                + (reason ?? string.Empty)
                + ".");
        }

        private static void LogStructuralBranchEvidence(
            IEnumerable<AnchorRow> rows,
            Stopwatch stopwatch)
        {
            List<StructuralBranchEvidenceRow> evidence =
                BuildStructuralBranchEvidence(rows);
            StructuralOwnerSelectionResult selectionResult =
                StructuralBranchEvidenceAnalyzer.AnalyseAndSelect(evidence);
            IReadOnlyList<StructuralBranchCluster> clusters = selectionResult.Clusters;

            int singleRidClusters = clusters.Count(value => value.IsHomogeneous);
            int selectedOwnerBranches = selectionResult.Selections
                .Select(value => value.BranchKey)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            int mixedRidClusters = clusters.Count(value =>
                value.DistinctLeafRidCount > 1);

            Log(
                "SOURCE structural branch evidence",
                stopwatch,
                "evidenceRows=" + evidence.Count
                + "; branchClusters=" + clusters.Count
                + "; singleRidClusters=" + singleRidClusters
                + "; mixedRidClusters=" + mixedRidClusters
                + "; selectedLeafRows=" + selectionResult.Selections.Count
                + "; selectedOwnerBranches=" + selectedOwnerBranches
                + "; noCandidateRows=" + selectionResult.NoCandidateRowCount
                + "; ambiguousNearestRows=" + selectionResult.AmbiguousNearestRowCount
                + "; selected={" + string.Join(
                    " || ",
                    selectionResult.Selections
                        .GroupBy(value => value.BranchKey, StringComparer.OrdinalIgnoreCase)
                        .Select(group => new { Branch = group.First().BranchName, Rid = group.First().Rid, Depth = group.Min(value => value.OwnerDepthFromAnchor), Leaves = group.Count(), Supporting = group.Max(value => value.SupportingLeafCount) })
                        .OrderByDescending(value => value.Leaves)
                        .Take(20)
                        .Select(value => "branch=" + value.Branch + ", rid=" + value.Rid + ", selectedLeaves=" + value.Leaves + ", supportingLeaves=" + value.Supporting + ", depth=" + value.Depth))
                + "}; top={" + string.Join(
                    " || ",
                    clusters.Take(20).Select(cluster =>
                        "branch=" + cluster.BranchName
                        + ", leaves=" + cluster.SupportingLeafCount
                        + ", distinctRids=" + cluster.DistinctLeafRidCount
                        + ", depth=" + cluster.MinimumDepth
                        + "-" + cluster.MaximumDepth
                        + ", branchRidMatches=" + cluster.BranchRidMatchCount
                        + ", rids=[" + string.Join(",", cluster.LeafRids.Take(5)) + "]"))
                + "}");
        }

        private static List<StructuralBranchEvidenceRow>
            BuildStructuralBranchEvidence(IEnumerable<AnchorRow> rows)
        {
            var result = new List<StructuralBranchEvidenceRow>();

            foreach (AnchorRow row in rows ?? Enumerable.Empty<AnchorRow>())
            {
                if (row == null
                    || row.Snapshot == null
                    || row.Snapshot.HasChildren
                    || string.IsNullOrWhiteSpace(row.Rid)
                    || string.Equals(row.Rid.Trim(), "\"\"", StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (ExternalTopologyPathNodeSnapshot node in
                    row.Snapshot.AncestorPath
                        ?? new List<ExternalTopologyPathNodeSnapshot>())
                {
                    if (node == null
                        || node.DepthFromItem <= 0
                        || !node.HasChildren)
                    {
                        continue;
                    }

                    string branchKey = BuildStructuralBranchKey(node);
                    string branchRid = GetSnapshotPathNodeRid(
                        row.Snapshot,
                        node.DepthFromItem);

                    result.Add(
                        new StructuralBranchEvidenceRow(
                            row.RowIndex,
                            row.Rid,
                            node.DepthFromItem,
                            branchKey,
                            node.DisplayName,
                            branchRid));
                }
            }

            return result;
        }

        private static string BuildStructuralBranchKey(
            ExternalTopologyPathNodeSnapshot node)
        {
            return string.Join(
                "|",
                new[]
                {
                    node.InstanceGuid ?? string.Empty,
                    node.ItemGuid ?? string.Empty,
                    node.SourceFile ?? string.Empty,
                    node.DisplayName ?? string.Empty,
                    node.DepthFromItem.ToString(CultureInfo.InvariantCulture)
                }).ToUpperInvariant();
        }

        private static string GetSnapshotPathNodeRid(
            ExternalModelItemSnapshot snapshot,
            int depthFromAnchor)
        {
            // The current transport stores RID properties only on the extracted
            // item snapshot. Ancestor RID values are not serialised per path
            // node, so this diagnostic intentionally reports blank here rather
            // than inventing evidence. T04-C tests structural clustering first.
            return string.Empty;
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
            if (snapshot == null)
            {
                return new TopologyItemFingerprint(
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty);
            }

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
