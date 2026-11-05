using System.Collections.Generic;
using NavisLegacyPlugin.Models.Topology;
namespace NavisLegacyPlugin.Topology.Tests
{
    internal static class TestData
    {
        internal static TopologyItemFingerprint F(string name) { return new TopologyItemFingerprint("i-"+name,"g-"+name,"f.nwd",name); }
        internal static List<TopologyPathNode> Path(params string[] names) { var r=new List<TopologyPathNode>(); for(int i=0;i<names.Length;i++) r.Add(new TopologyPathNode(i,names[i],F(names[i]))); return r; }
        internal static TopologyAnchorEvidence Anchor(TopologyAnchorStatus s=TopologyAnchorStatus.Matched) { return new TopologyAnchorEvidence("IfcGuid","key",s,"reason",F("S"),F("T")); }
        internal static TopologyCandidateEvidence Candidate(int score,string name="C") { return new TopologyCandidateEvidence(F(name),score,Path(name),new TopologyAlignmentStep[0]); }
    }
}