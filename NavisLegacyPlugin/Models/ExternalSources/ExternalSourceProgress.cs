using System.Runtime.Serialization;
namespace NavisLegacyPlugin.Models.ExternalSources
{
    public static class ExternalSourceProgressStage
    {
        public const string StartingExternalSource = "StartingExternalSource";
        public const string OpeningExternalFile = "OpeningExternalFile";
        public const string LocatingExportSet = "LocatingExportSet";
        public const string ResolvingSourcePopulation = "ResolvingSourcePopulation";
        public const string ReadingSourceData = "ReadingSourceData";
        public const string VerifyingSource = "VerifyingSource";
        public const string ExternalSourceReady = "ExternalSourceReady";
    }
    [DataContract]
    public sealed class ExternalSourceProgress
    {
        [DataMember(Order = 1)] public string Stage { get; set; }
        [DataMember(Order = 2)] public int Current { get; set; }
        [DataMember(Order = 3)] public int Total { get; set; }
        [DataMember(Order = 4)] public string Message { get; set; }
        [DataMember(Order = 5)] public bool IsDeterminate { get; set; }
    }
}
