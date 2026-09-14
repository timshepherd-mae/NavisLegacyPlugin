using System.Threading.Tasks;
using NavisLegacyPlugin.Models;

namespace NavisLegacyPlugin.Services.Execution
{
    public sealed class ExecuteSignatureExecutor
    {
        public async Task<ExecuteResult> ExecuteAsync(
            ExecuteSignature signature)
        {
            // implementation comes next

            return new ExecuteResult(
                0,
                0,
                0,
                0);
        }
    }
}
