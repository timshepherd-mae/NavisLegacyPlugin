using Autodesk.Navisworks.Api.Automation;
using NavisLegacyPlugin.Models.ExternalSources;
using System;
using System.IO;
using System.Runtime.Serialization.Json;

namespace NavisLegacyPlugin.Services.ExternalSources
{
    public sealed class ExternalExportPopulationService : IExternalExportPopulationService
    {
        public const string WorkerPluginId = "Phase63ExternalPopulationWorker.MAE";

        public ExternalExportPopulationResponse Resolve(ExternalExportPopulationRequest request)
        {
            if (request == null) throw new ArgumentNullException("request");
            if (string.IsNullOrWhiteSpace(request.FilePath) || !File.Exists(request.FilePath))
                throw new ArgumentException("External source file does not exist.", "request");
            if (string.IsNullOrWhiteSpace(request.ExportSetName))
                throw new ArgumentException("Export set name is required.", "request");

            string responsePath = Path.Combine(Path.GetTempPath(), "NavisLegacy_Phase63_" + Guid.NewGuid().ToString("N") + ".json");
            NavisworksApplication automation = null;
            try
            {
                automation = new NavisworksApplication();
                automation.OpenFile(request.FilePath);
                automation.ExecuteAddInPlugin(WorkerPluginId, "WORKER", responsePath,
                    request.FilePath, request.ExportSetName, request.ResolutionType.ToString());
                if (!File.Exists(responsePath)) throw new InvalidOperationException("External worker did not create a response.");
                using (FileStream stream = File.OpenRead(responsePath))
                    return (ExternalExportPopulationResponse)new DataContractJsonSerializer(typeof(ExternalExportPopulationResponse)).ReadObject(stream);
            }
            finally
            {
                if (automation != null) automation.Dispose();
                try { if (File.Exists(responsePath)) File.Delete(responsePath); } catch { }
            }
        }
    }
}
