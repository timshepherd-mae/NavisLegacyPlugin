using System.Collections.Generic;
using System.Runtime.Serialization;

namespace NavisLegacyPlugin.Models.ExternalSources
{
    [DataContract]
    public sealed class ExternalModelItemSnapshot
    {
        public ExternalModelItemSnapshot() { Properties = new List<ExternalPropertySnapshot>(); }
        [DataMember(Order = 1)] public string InstanceGuid { get; set; }
        [DataMember(Order = 2)] public string DisplayName { get; set; }
        [DataMember(Order = 3)] public bool HasChildren { get; set; }
        [DataMember(Order = 4)] public List<ExternalPropertySnapshot> Properties { get; set; }
    }

    [DataContract]
    public sealed class ExternalPropertySnapshot
    {
        [DataMember(Order = 1)] public string Category { get; set; }
        [DataMember(Order = 2)] public string Name { get; set; }
        [DataMember(Order = 3)] public string Value { get; set; }
    }
}
