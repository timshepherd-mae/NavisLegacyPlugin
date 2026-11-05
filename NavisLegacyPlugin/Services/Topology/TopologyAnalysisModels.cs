using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Navisworks.Api;
using NavisLegacyPlugin.Models.Topology;

namespace NavisLegacyPlugin.Services.Topology
{
    public sealed class TopologySourceGroup
    {
        public TopologySourceGroup(string groupId, string ridValue, TopologyItemFingerprint sourceOwner,
            IEnumerable<TopologyPathNode> sourcePath, IEnumerable<TopologyAnchorEvidence> anchors)
        {
            GroupId = groupId ?? string.Empty;
            RidValue = ridValue ?? string.Empty;
            SourceOwner = sourceOwner;
            SourcePath = (sourcePath ?? Enumerable.Empty<TopologyPathNode>()).ToList().AsReadOnly();
            Anchors = (anchors ?? Enumerable.Empty<TopologyAnchorEvidence>()).ToList().AsReadOnly();
        }
        public string GroupId { get; private set; }
        public string RidValue { get; private set; }
        public TopologyItemFingerprint SourceOwner { get; private set; }
        public IReadOnlyList<TopologyPathNode> SourcePath { get; private set; }
        public IReadOnlyList<TopologyAnchorEvidence> Anchors { get; private set; }
    }

    public sealed class TopologyTargetCandidate
    {
        public TopologyTargetCandidate(ModelItem item, TopologyItemFingerprint fingerprint,
            IEnumerable<TopologyPathNode> path)
        {
            Item = item ?? throw new ArgumentNullException("item");
            Fingerprint = fingerprint ?? throw new ArgumentNullException("fingerprint");
            Path = (path ?? Enumerable.Empty<TopologyPathNode>()).ToList().AsReadOnly();
        }
        public ModelItem Item { get; private set; }
        public TopologyItemFingerprint Fingerprint { get; private set; }
        public IReadOnlyList<TopologyPathNode> Path { get; private set; }
    }

    public sealed class TopologyGroupAnalysis
    {
        public TopologyGroupAnalysis(TopologySourceGroup source, TopologyResolution resolution,
            TopologyTargetCandidate selectedCandidate)
        {
            Source = source ?? throw new ArgumentNullException("source");
            Resolution = resolution ?? throw new ArgumentNullException("resolution");
            SelectedCandidate = selectedCandidate;
        }
        public TopologySourceGroup Source { get; private set; }
        public TopologyResolution Resolution { get; private set; }
        public TopologyTargetCandidate SelectedCandidate { get; private set; }
    }
}
