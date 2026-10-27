using NavisLegacyPlugin.Models.Execution; 
namespace NavisLegacyPlugin.Models { 
    public sealed class ExecuteResult { 
        public ExecuteResult(TransferOutcomeSet outcomes){
            Outcomes=outcomes;
            Matched=outcomes==null?0:outcomes.Matched;
            Unmatched=outcomes==null?0:outcomes.Unmatched;
            Ambiguous=outcomes==null?0:outcomes.Ambiguous;
            MissingKey=outcomes==null?0:outcomes.MissingKey;
            Written=outcomes==null?0:outcomes.Written;
            Skipped=outcomes==null?0:outcomes.Skipped;
            Failed=outcomes==null?0:outcomes.Failed;
            NotApplicable=outcomes==null?0:outcomes.NotApplicable;
        } 
        public ExecuteResult(int matched,int unmatched,int written,int skipped){
            Matched=matched;
            Unmatched=unmatched;
            Written=written;
            Skipped=skipped;
        } 
        public TransferOutcomeSet Outcomes{get;} 
        public int Matched{get;} 
        public int Unmatched{get;} 
        public int Ambiguous{get;} 
        public int MissingKey{get;} 
        public int Written{get;} 
        public int Skipped{get;} 
        public int Failed{get;} 
        public int NotApplicable{get;}
    }
}
