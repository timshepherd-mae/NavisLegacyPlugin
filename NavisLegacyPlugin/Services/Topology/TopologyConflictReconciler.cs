using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Navisworks.Api;
using NavisLegacyPlugin.Models.Topology;
using NavisLegacyPlugin.Services.Collections;

namespace NavisLegacyPlugin.Services.Topology
{
    public sealed class TopologyReconciliationResult
    {
        public TopologyReconciliationResult(IEnumerable<TopologyWritePlan> plans, IEnumerable<TopologyGroupAnalysis> blocked)
        {
            Plans = (plans ?? Enumerable.Empty<TopologyWritePlan>()).ToList().AsReadOnly();
            Blocked = (blocked ?? Enumerable.Empty<TopologyGroupAnalysis>()).ToList().AsReadOnly();
        }
        public IReadOnlyList<TopologyWritePlan> Plans { get; private set; }
        public IReadOnlyList<TopologyGroupAnalysis> Blocked { get; private set; }
    }

    public sealed class TopologyWritePlan
    {
        public TopologyWritePlan(ModelItem target, string ridValue, IEnumerable<TopologyGroupAnalysis> sources)
        {
            Target = target ?? throw new ArgumentNullException("target");
            RidValue = ridValue ?? string.Empty;
            Sources = (sources ?? Enumerable.Empty<TopologyGroupAnalysis>()).ToList().AsReadOnly();
        }
        public ModelItem Target { get; private set; }
        public string RidValue { get; private set; }
        public IReadOnlyList<TopologyGroupAnalysis> Sources { get; private set; }
    }

    public sealed class TopologyConflictReconciler
    {
        public TopologyReconciliationResult Reconcile(IEnumerable<TopologyGroupAnalysis> analyses)
        {
            List<TopologyGroupAnalysis> all = (analyses ?? Enumerable.Empty<TopologyGroupAnalysis>()).ToList();
            List<TopologyGroupAnalysis> eligible = all.Where(x => x != null && x.Resolution != null
                && x.Resolution.Status == TopologyResolutionStatus.Good && x.Resolution.WriteEligible
                && x.SelectedCandidate != null && x.SelectedCandidate.Item != null
                && !string.IsNullOrWhiteSpace(x.Source.RidValue)).ToList();
            var plans = new List<TopologyWritePlan>();
            var blocked = new List<TopologyGroupAnalysis>();
            foreach (IGrouping<ModelItem, TopologyGroupAnalysis> targetGroup in eligible.GroupBy(
                x => x.SelectedCandidate.Item, ModelItemReferenceComparer.Instance))
            {
                List<IGrouping<string, TopologyGroupAnalysis>> ridGroups = targetGroup.GroupBy(
                    x => x.Source.RidValue.Trim(), StringComparer.OrdinalIgnoreCase).ToList();
                if (ridGroups.Count != 1)
                {
                    blocked.AddRange(targetGroup);
                    continue;
                }
                IGrouping<string, TopologyGroupAnalysis> ridGroup = ridGroups[0];
                plans.Add(new TopologyWritePlan(targetGroup.Key, ridGroup.Key, ridGroup));
            }
            blocked.AddRange(all.Except(eligible));
            return new TopologyReconciliationResult(plans, blocked.Distinct().ToList());
        }
    }
}
