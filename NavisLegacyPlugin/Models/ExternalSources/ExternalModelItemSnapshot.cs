using System.Collections.Generic;
using System.Runtime.Serialization;
namespace NavisLegacyPlugin.Models.ExternalSources
{
    [DataContract] public sealed class ExternalTopologyPathNodeSnapshot
    {
        [DataMember(Order=1)] public int DepthFromItem{get;set;}
        [DataMember(Order=2)] public string InstanceGuid{get;set;}
        [DataMember(Order=3)] public string DisplayName{get;set;}
        [DataMember(Order=4)] public string ItemGuid{get;set;}
        [DataMember(Order=5)] public string SourceFile{get;set;}
    }
    [DataContract] public sealed class ExternalModelItemSnapshot
    {
        public ExternalModelItemSnapshot(){Properties=new List<ExternalPropertySnapshot>();AncestorPath=new List<ExternalTopologyPathNodeSnapshot>();}
        [DataMember(Order=1)] public string InstanceGuid{get;set;}
        [DataMember(Order=2)] public string DisplayName{get;set;}
        [DataMember(Order=3)] public bool HasChildren{get;set;}
        [DataMember(Order=4)] public List<ExternalPropertySnapshot> Properties{get;set;}
        [DataMember(Order=5)] public List<ExternalTopologyPathNodeSnapshot> AncestorPath{get;set;}
    }
    [DataContract] public sealed class ExternalPropertySnapshot
    {
        [DataMember(Order=1)] public string Category{get;set;}
        [DataMember(Order=2)] public string Name{get;set;}
        [DataMember(Order=3)] public string Value{get;set;}
    }
}
