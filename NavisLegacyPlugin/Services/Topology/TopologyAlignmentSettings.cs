using System;

namespace NavisLegacyPlugin.Services.Topology
{
    public sealed class TopologyAlignmentSettings
    {
        public TopologyAlignmentSettings()
        {
            GoodThreshold = 80;
            VagueThreshold = 50;
            MinimumSeparation = 15;
            ExactScore = 100;
            SubstitutionScore = 35;
            InsertionScore = 20;
            DeletionScore = 20;
            AnchorWeight = 40;
            OwnerWeight = 32;
            MinimumContextWeight = 8;
            ContextWeightReduction = 6;
            MaximumRepresentativeAnchorsPerRoute = 5;
        }

        public int GoodThreshold { get; set; }
        public int VagueThreshold { get; set; }
        public int MinimumSeparation { get; set; }
        public int ExactScore { get; set; }
        public int SubstitutionScore { get; set; }
        public int InsertionScore { get; set; }
        public int DeletionScore { get; set; }
        public int AnchorWeight { get; set; }
        public int OwnerWeight { get; set; }
        public int MinimumContextWeight { get; set; }
        public int ContextWeightReduction { get; set; }
        public int MaximumRepresentativeAnchorsPerRoute { get; set; }

        public void Validate()
        {
            if (GoodThreshold < 0 || GoodThreshold > 100) throw new InvalidOperationException("GoodThreshold must be between 0 and 100.");
            if (VagueThreshold < 0 || VagueThreshold > GoodThreshold) throw new InvalidOperationException("VagueThreshold must be between 0 and GoodThreshold.");
            if (MinimumSeparation < 0 || MinimumSeparation > 100) throw new InvalidOperationException("MinimumSeparation must be between 0 and 100.");
            if (AnchorWeight <= 0 || OwnerWeight <= 0 || MinimumContextWeight <= 0) throw new InvalidOperationException("Topology weights must be positive.");
            if (MaximumRepresentativeAnchorsPerRoute < 1) throw new InvalidOperationException("MaximumRepresentativeAnchorsPerRoute must be at least 1.");
        }
    }
}
