using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Navisworks.Api;
using NavisLegacyPlugin.Models.Execution;
using NavisLegacyPlugin.Services.Collections;

namespace NavisLegacyPlugin.Services.Topology
{
    public sealed class TopologyWriteExecution
    {
        public TopologyWriteExecution(TopologyWritePlan plan, TransferOperationStatus status, string reason, string error)
        {
            Plan = plan; Status = status; Reason = reason ?? string.Empty; Error = error ?? string.Empty;
        }
        public TopologyWritePlan Plan { get; private set; }
        public TransferOperationStatus Status { get; private set; }
        public string Reason { get; private set; }
        public string Error { get; private set; }
    }

    public sealed class TopologyBranchWriteExecutor
    {
        private readonly ComPropertyWriteService _writer;
        public TopologyBranchWriteExecutor(ComPropertyWriteService writer)
        {
            _writer = writer ?? throw new ArgumentNullException("writer");
        }

        public IReadOnlyList<TopologyWriteExecution> Execute(TopologyReconciliationResult reconciliation,
            IEnumerable<ModelItem> resolvedTargetPopulation, bool overwrite)
        {
            if (reconciliation == null) throw new ArgumentNullException("reconciliation");
            var targetBoundary = new HashSet<ModelItem>((resolvedTargetPopulation ?? Enumerable.Empty<ModelItem>())
                .Where(x => x != null), ModelItemReferenceComparer.Instance);
            var results = new List<TopologyWriteExecution>();
            foreach (TopologyWritePlan plan in reconciliation.Plans)
            {
                if (!targetBoundary.Contains(plan.Target))
                {
                    results.Add(new TopologyWriteExecution(plan, TransferOperationStatus.Failed,
                        "Final revalidation rejected an item outside the resolved TARGET population.", string.Empty));
                    continue;
                }
                if (string.IsNullOrWhiteSpace(plan.RidValue))
                {
                    results.Add(new TopologyWriteExecution(plan, TransferOperationStatus.NotApplicable,
                        "The reconciled RID is blank.", string.Empty));
                    continue;
                }
                DataProperty existing = plan.Target.PropertyCategories.FindCategoryByDisplayName("MAE-4D")?
                    .Properties.FindPropertyByDisplayName("RID");
                if (!overwrite && existing != null)
                {
                    results.Add(new TopologyWriteExecution(plan, TransferOperationStatus.Skipped,
                        "Existing RID preserved by overwrite policy.", string.Empty));
                    continue;
                }
                try
                {
                    _writer.WriteUserDefinedProperties(plan.Target, "MAE-4D",
                        new Dictionary<string, string> { { "RID", plan.RidValue } });
                    results.Add(new TopologyWriteExecution(plan, TransferOperationStatus.Written,
                        "RID written after complete analysis, reconciliation and final revalidation.", string.Empty));
                }
                catch (Exception exception)
                {
                    results.Add(new TopologyWriteExecution(plan, TransferOperationStatus.Failed,
                        "RID write failed.", exception.Message));
                }
            }
            return results.AsReadOnly();
        }
    }
}
