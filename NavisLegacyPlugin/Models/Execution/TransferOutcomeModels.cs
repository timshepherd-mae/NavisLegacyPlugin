using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.Serialization;
namespace NavisLegacyPlugin.Models.Execution
{
    public enum TransferMatchStatus { Matched, Unmatched, Ambiguous, MissingKey }
    public enum TransferOperationStatus { Written, Skipped, Failed, NotApplicable }
    public enum TransferExecutionStatus { Completed, CompletedWithFailures, Failed }
    [DataContract] public sealed class TransferSourceIdentity
    {
        public TransferSourceIdentity(int rowIndex,string snapshotInstanceGuid,string itemGuid,string itemSourceFile,string displayName){RowIndex=rowIndex;SnapshotInstanceGuid=snapshotInstanceGuid??string.Empty;ItemGuid=itemGuid??string.Empty;ItemSourceFile=itemSourceFile??string.Empty;DisplayName=displayName??string.Empty;}
        [DataMember(Order=1)] public int RowIndex{get;private set;} [DataMember(Order=2)] public string SnapshotInstanceGuid{get;private set;} [DataMember(Order=3)] public string ItemGuid{get;private set;} [DataMember(Order=4)] public string ItemSourceFile{get;private set;} [DataMember(Order=5)] public string DisplayName{get;private set;}
    }
    [DataContract] public sealed class TransferPayloadValue { public TransferPayloadValue(string name,string value){Name=name??string.Empty;Value=value??string.Empty;} [DataMember(Order=1)] public string Name{get;private set;} [DataMember(Order=2)] public string Value{get;private set;} }
    [DataContract] public sealed class TransferTargetFingerprint
    { public TransferTargetFingerprint(string instanceGuid,string itemGuid,string itemSourceFile,string displayName){InstanceGuid=instanceGuid??string.Empty;ItemGuid=itemGuid??string.Empty;ItemSourceFile=itemSourceFile??string.Empty;DisplayName=displayName??string.Empty;} [DataMember(Order=1)] public string InstanceGuid{get;private set;} [DataMember(Order=2)] public string ItemGuid{get;private set;} [DataMember(Order=3)] public string ItemSourceFile{get;private set;} [DataMember(Order=4)] public string DisplayName{get;private set;} }
    [DataContract] public sealed class TransferMatchResult
    { public TransferMatchResult(TransferMatchStatus status,string strategyName,string matchKey,string reason,TransferTargetFingerprint target){Status=status;StrategyName=strategyName??string.Empty;MatchKey=matchKey??string.Empty;Reason=reason??string.Empty;Target=target;} [DataMember(Order=1)] public TransferMatchStatus Status{get;private set;} [DataMember(Order=2)] public string StrategyName{get;private set;} [DataMember(Order=3)] public string MatchKey{get;private set;} [DataMember(Order=4)] public string Reason{get;private set;} [DataMember(Order=5)] public TransferTargetFingerprint Target{get;private set;} }
    [DataContract] public sealed class TransferOperationResult
    { public TransferOperationResult(TransferOperationStatus status,string operationType,string tab,string property,string value,string reason,string error){Status=status;OperationType=operationType??string.Empty;Tab=tab??string.Empty;Property=property??string.Empty;Value=value??string.Empty;Reason=reason??string.Empty;Error=error??string.Empty;} [DataMember(Order=1)] public TransferOperationStatus Status{get;private set;} [DataMember(Order=2)] public string OperationType{get;private set;} [DataMember(Order=3)] public string Tab{get;private set;} [DataMember(Order=4)] public string Property{get;private set;} [DataMember(Order=5)] public string Value{get;private set;} [DataMember(Order=6)] public string Reason{get;private set;} [DataMember(Order=7)] public string Error{get;private set;} }
    [DataContract] public sealed class TransferOutcome
    {
        public TransferOutcome(TransferSourceIdentity source,IEnumerable<TransferPayloadValue> payload,TransferMatchResult match,IEnumerable<TransferOperationResult> operations){Source=source??throw new ArgumentNullException("source");Payload=new ReadOnlyCollection<TransferPayloadValue>((payload??Enumerable.Empty<TransferPayloadValue>()).ToList());Match=match??throw new ArgumentNullException("match");Operations=new ReadOnlyCollection<TransferOperationResult>((operations??Enumerable.Empty<TransferOperationResult>()).ToList());OperationStatus=Derive(Operations);}
        [DataMember(Order=1)] public TransferSourceIdentity Source{get;private set;} [DataMember(Order=2)] public ReadOnlyCollection<TransferPayloadValue> Payload{get;private set;} [DataMember(Order=3)] public TransferMatchResult Match{get;private set;} [DataMember(Order=4)] public ReadOnlyCollection<TransferOperationResult> Operations{get;private set;} [DataMember(Order=5)] public TransferOperationStatus OperationStatus{get;private set;}
        private static TransferOperationStatus Derive(IEnumerable<TransferOperationResult> x){var s=x.Select(y=>y.Status).ToList(); if(s.Contains(TransferOperationStatus.Failed))return TransferOperationStatus.Failed;if(s.Contains(TransferOperationStatus.Written))return TransferOperationStatus.Written;if(s.Contains(TransferOperationStatus.Skipped))return TransferOperationStatus.Skipped;return TransferOperationStatus.NotApplicable;}
    }
}
