namespace NavisLegacyPlugin.Models.Topology
{
    public sealed class TopologyReviewRow
    {
        public int SourceRowIndex { get; set; }
        public string SourceOwner { get; set; }
        public string RidValue { get; set; }
        public string MatchingStrategy { get; set; }
        public string AnchorMethod { get; set; }
        public string AnchorStatus { get; set; }
        public string AnchorKey { get; set; }
        public string Status { get; set; }
        public bool Conflict { get; set; }
        public int BestScore { get; set; }
        public int NextBestScore { get; set; }
        public int Separation { get { return BestScore - NextBestScore; } }
        public string TargetOwner { get; set; }
        public string OperationStatus { get; set; }
        public string Reason { get; set; }
    }
}
