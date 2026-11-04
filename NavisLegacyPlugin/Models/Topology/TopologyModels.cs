using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.Serialization;

namespace NavisLegacyPlugin.Models.Topology
{
    public enum TopologyResolutionStatus
    {
        Good,
        Vague,
        Ambiguous,
        Unresolved
    }

    public enum TopologyAlignmentDisposition
    {
        Exact,
        Substituted,
        Inserted,
        Deleted
    }

    public enum TopologyAnchorStatus
    {
        Matched,
        MissingKey,
        Unmatched,
        Ambiguous
    }

    [DataContract]
    public sealed class TopologyItemFingerprint
    {
        public TopologyItemFingerprint(string instanceGuid, string itemGuid, string sourceFile, string displayName)
        {
            InstanceGuid = instanceGuid ?? string.Empty;
            ItemGuid = itemGuid ?? string.Empty;
            SourceFile = sourceFile ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
        }

        [DataMember(Order = 1)] public string InstanceGuid { get; private set; }
        [DataMember(Order = 2)] public string ItemGuid { get; private set; }
        [DataMember(Order = 3)] public string SourceFile { get; private set; }
        [DataMember(Order = 4)] public string DisplayName { get; private set; }
    }

    [DataContract]
    public sealed class TopologyPathNode
    {
        public TopologyPathNode(int depthFromAnchor, string displayName, TopologyItemFingerprint fingerprint)
        {
            DepthFromAnchor = depthFromAnchor;
            DisplayName = displayName ?? string.Empty;
            Fingerprint = fingerprint;
        }

        [DataMember(Order = 1)] public int DepthFromAnchor { get; private set; }
        [DataMember(Order = 2)] public string DisplayName { get; private set; }
        [DataMember(Order = 3)] public TopologyItemFingerprint Fingerprint { get; private set; }
    }

    [DataContract]
    public sealed class TopologyAnchorEvidence
    {
        public TopologyAnchorEvidence(string strategyName, string normalizedKey, TopologyAnchorStatus status,
            string reason, TopologyItemFingerprint sourceAnchor, TopologyItemFingerprint targetAnchor)
        {
            StrategyName = strategyName ?? string.Empty;
            NormalizedKey = normalizedKey ?? string.Empty;
            Status = status;
            Reason = reason ?? string.Empty;
            SourceAnchor = sourceAnchor;
            TargetAnchor = targetAnchor;
        }

        [DataMember(Order = 1)] public string StrategyName { get; private set; }
        [DataMember(Order = 2)] public string NormalizedKey { get; private set; }
        [DataMember(Order = 3)] public TopologyAnchorStatus Status { get; private set; }
        [DataMember(Order = 4)] public string Reason { get; private set; }
        [DataMember(Order = 5)] public TopologyItemFingerprint SourceAnchor { get; private set; }
        [DataMember(Order = 6)] public TopologyItemFingerprint TargetAnchor { get; private set; }
    }

    [DataContract]
    public sealed class TopologyAlignmentStep
    {
        public TopologyAlignmentStep(int sourceIndex, int targetIndex, string sourceName, string targetName,
            TopologyAlignmentDisposition disposition, int scoreContribution, int weight)
        {
            SourceIndex = sourceIndex;
            TargetIndex = targetIndex;
            SourceName = sourceName ?? string.Empty;
            TargetName = targetName ?? string.Empty;
            Disposition = disposition;
            ScoreContribution = scoreContribution;
            Weight = weight;
        }

        [DataMember(Order = 1)] public int SourceIndex { get; private set; }
        [DataMember(Order = 2)] public int TargetIndex { get; private set; }
        [DataMember(Order = 3)] public string SourceName { get; private set; }
        [DataMember(Order = 4)] public string TargetName { get; private set; }
        [DataMember(Order = 5)] public TopologyAlignmentDisposition Disposition { get; private set; }
        [DataMember(Order = 6)] public int ScoreContribution { get; private set; }
        [DataMember(Order = 7)] public int Weight { get; private set; }
    }

    [DataContract]
    public sealed class TopologyCandidateEvidence
    {
        public TopologyCandidateEvidence(TopologyItemFingerprint candidate, int score,
            IEnumerable<TopologyPathNode> targetPath, IEnumerable<TopologyAlignmentStep> alignment)
        {
            Candidate = candidate;
            Score = score;
            TargetPath = new ReadOnlyCollection<TopologyPathNode>((targetPath ?? Enumerable.Empty<TopologyPathNode>()).ToList());
            Alignment = new ReadOnlyCollection<TopologyAlignmentStep>((alignment ?? Enumerable.Empty<TopologyAlignmentStep>()).ToList());
        }

        [DataMember(Order = 1)] public TopologyItemFingerprint Candidate { get; private set; }
        [DataMember(Order = 2)] public int Score { get; private set; }
        [DataMember(Order = 3)] public ReadOnlyCollection<TopologyPathNode> TargetPath { get; private set; }
        [DataMember(Order = 4)] public ReadOnlyCollection<TopologyAlignmentStep> Alignment { get; private set; }
    }

    [DataContract]
    public sealed class TopologyResolution
    {
        public TopologyResolution(TopologyResolutionStatus status, string reason, bool writeEligible,
            int bestScore, int nextBestScore, IEnumerable<TopologyAnchorEvidence> anchors,
            TopologyItemFingerprint sourceRidOwner, string ridValue,
            IEnumerable<TopologyPathNode> sourcePath, IEnumerable<TopologyCandidateEvidence> candidates)
        {
            Status = status;
            Reason = reason ?? string.Empty;
            WriteEligible = writeEligible;
            BestScore = bestScore;
            NextBestScore = nextBestScore;
            Separation = Math.Max(0, bestScore - nextBestScore);
            Anchors = new ReadOnlyCollection<TopologyAnchorEvidence>((anchors ?? Enumerable.Empty<TopologyAnchorEvidence>()).ToList());
            SourceRidOwner = sourceRidOwner;
            RidValue = ridValue ?? string.Empty;
            SourcePath = new ReadOnlyCollection<TopologyPathNode>((sourcePath ?? Enumerable.Empty<TopologyPathNode>()).ToList());
            Candidates = new ReadOnlyCollection<TopologyCandidateEvidence>((candidates ?? Enumerable.Empty<TopologyCandidateEvidence>()).ToList());
        }

        [DataMember(Order = 1)] public TopologyResolutionStatus Status { get; private set; }
        [DataMember(Order = 2)] public string Reason { get; private set; }
        [DataMember(Order = 3)] public bool WriteEligible { get; private set; }
        [DataMember(Order = 4)] public int BestScore { get; private set; }
        [DataMember(Order = 5)] public int NextBestScore { get; private set; }
        [DataMember(Order = 6)] public int Separation { get; private set; }
        [DataMember(Order = 7)] public ReadOnlyCollection<TopologyAnchorEvidence> Anchors { get; private set; }
        [DataMember(Order = 8)] public TopologyItemFingerprint SourceRidOwner { get; private set; }
        [DataMember(Order = 9)] public string RidValue { get; private set; }
        [DataMember(Order = 10)] public ReadOnlyCollection<TopologyPathNode> SourcePath { get; private set; }
        [DataMember(Order = 11)] public ReadOnlyCollection<TopologyCandidateEvidence> Candidates { get; private set; }
    }
}
