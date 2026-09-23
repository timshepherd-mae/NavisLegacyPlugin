using System.Collections.Generic;
using System.Runtime.Serialization;

namespace NavisLegacyPlugin.Models.ExternalSources
{
    [DataContract]
    public sealed class ExternalExportPopulationResponse
    {
        public ExternalExportPopulationResponse() { Items = new List<ExternalModelItemSnapshot>(); }
        [DataMember(Order = 1)] public bool Success { get; set; }
        [DataMember(Order = 2)] public string Error { get; set; }
        [DataMember(Order = 3)] public string SourceFile { get; set; }
        [DataMember(Order = 4)] public string ExportSetName { get; set; }
        [DataMember(Order = 5)] public string ResolutionType { get; set; }
        [DataMember(Order = 6)] public int AllCount { get; set; }
        [DataMember(Order = 7)] public int BranchCount { get; set; }
        [DataMember(Order = 8)] public int LeafCount { get; set; }
        [DataMember(Order = 9)] public List<ExternalModelItemSnapshot> Items { get; set; }
        [DataMember(Order = 10)] public string SourceHashBefore { get; set; }
        [DataMember(Order = 11)] public string SourceHashAfter { get; set; }
    }
}
