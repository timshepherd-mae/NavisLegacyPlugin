using System;
using System.Collections.Generic;
using Autodesk.Navisworks.Api;

namespace NavisLegacyPlugin.Services.Topology
{
    public sealed class TopologyDropBResult
    {
        public TopologyDropBResult(IReadOnlyList<TopologyGroupAnalysis> analyses,
            TopologyReconciliationResult reconciliation, IReadOnlyList<TopologyWriteExecution> writes)
        { Analyses = analyses; Reconciliation = reconciliation; Writes = writes; }
        public IReadOnlyList<TopologyGroupAnalysis> Analyses { get; private set; }
        public TopologyReconciliationResult Reconciliation { get; private set; }
        public IReadOnlyList<TopologyWriteExecution> Writes { get; private set; }
    }

    public sealed class TopologyDropBCoordinator
    {
        private readonly TopologyAnalysisService _analysis;
        private readonly TopologyConflictReconciler _reconciler;
        private readonly TopologyBranchWriteExecutor _writer;
        public TopologyDropBCoordinator(TopologyAnalysisService analysis,
            TopologyConflictReconciler reconciler, TopologyBranchWriteExecutor writer)
        { _analysis = analysis; _reconciler = reconciler; _writer = writer; }

        public TopologyDropBResult Execute(IEnumerable<TopologySourceGroup> sources,
            Func<TopologySourceGroup, IEnumerable<TopologyTargetCandidate>> candidateProvider,
            IEnumerable<ModelItem> resolvedTargetPopulation, bool overwrite)
        {
            if (candidateProvider == null) throw new ArgumentNullException("candidateProvider");
            var analyses = new List<TopologyGroupAnalysis>();
            foreach (TopologySourceGroup source in sources ?? new TopologySourceGroup[0])
                analyses.Add(_analysis.Analyse(source, candidateProvider(source)));
            TopologyReconciliationResult reconciliation = _reconciler.Reconcile(analyses);
            IReadOnlyList<TopologyWriteExecution> writes = _writer.Execute(reconciliation,
                resolvedTargetPopulation, overwrite);
            return new TopologyDropBResult(analyses.AsReadOnly(), reconciliation, writes);
        }
    }
}
