using Autodesk.Navisworks.Api.Automation;
using NavisLegacyPlugin.Models.ExternalSources;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;

namespace NavisLegacyPlugin.Services.ExternalSources
{
    public sealed class ExternalExportPopulationService
        : IExternalExportPopulationService
    {
        public const string WorkerPluginId =
            "Phase63ExternalPopulationWorker.MAE";

        private const int ProgressPollMilliseconds = 200;
        private const int MonitorShutdownWaitMilliseconds = 2000;

        private sealed class ProgressMonitorState
        {
            public volatile bool StopRequested;
        }

        public ExternalExportPopulationResponse Resolve(
            ExternalExportPopulationRequest request,
            IProgress<ExternalSourceProgress> progress = null)
        {
            if (request == null)
                throw new ArgumentNullException("request");

            if (string.IsNullOrWhiteSpace(request.FilePath) ||
                !File.Exists(request.FilePath))
            {
                throw new ArgumentException(
                    "External source file does not exist.",
                    "request");
            }

            if (string.IsNullOrWhiteSpace(request.ExportSetName))
            {
                throw new ArgumentException(
                    "Export set name is required.",
                    "request");
            }

            string token = Guid.NewGuid().ToString("N");

            string responsePath = Path.Combine(
                Path.GetTempPath(),
                "NavisLegacy_Phase72_" + token + ".response.json");

            string progressPath = Path.Combine(
                Path.GetTempPath(),
                "NavisLegacy_Phase72_" + token + ".progress.json");

            NavisworksApplication automation = null;
            var monitorState = new ProgressMonitorState();
            Task monitorTask = null;

            try
            {
                Report(
                    progress,
                    ExternalSourceProgressStage.StartingExternalSource,
                    0,
                    0,
                    "Preparing external SOURCE read...",
                    false);

                automation = new NavisworksApplication();

                Report(
                    progress,
                    ExternalSourceProgressStage.OpeningExternalFile,
                    0,
                    0,
                    "Opening external Navisworks file...",
                    false);

                automation.OpenFile(request.FilePath);

                monitorTask = MonitorProgressAsync(
                    progressPath,
                    progress,
                    monitorState);

                automation.ExecuteAddInPlugin(
                    WorkerPluginId,
                    "WORKER",
                    responsePath,
                    request.FilePath,
                    request.ExportSetName,
                    request.ResolutionType.ToString(),
                    progressPath);

                ReportFinalProgressIfAvailable(
                    progressPath,
                    progress);

                if (!File.Exists(responsePath))
                {
                    throw new InvalidOperationException(
                        "External worker did not create a response.");
                }

                using (FileStream stream = File.Open(
                    responsePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read))
                {
                    var serializer =
                        new DataContractJsonSerializer(
                            typeof(ExternalExportPopulationResponse));

                    return (ExternalExportPopulationResponse)
                        serializer.ReadObject(stream);
                }
            }
            finally
            {
                monitorState.StopRequested = true;
                StopMonitorSafely(monitorTask);

                if (automation != null)
                {
                    try
                    {
                        automation.Dispose();
                    }
                    catch (Exception exception)
                    {
                        Debug.WriteLine(
                            "[PHASE72] Automation disposal failed: " +
                            exception);
                    }
                }

                TryDelete(responsePath);
                TryDelete(progressPath);
                TryDelete(progressPath + ".tmp");
            }
        }

        private static async Task MonitorProgressAsync(
            string progressPath,
            IProgress<ExternalSourceProgress> progress,
            ProgressMonitorState state)
        {
            if (progress == null || state == null)
                return;

            string lastPayload = null;

            while (!state.StopRequested)
            {
                try
                {
                    if (File.Exists(progressPath))
                    {
                        string payload =
                            ReadProgressPayload(progressPath);

                        if (!string.IsNullOrWhiteSpace(payload) &&
                            !string.Equals(
                                lastPayload,
                                payload,
                                StringComparison.Ordinal))
                        {
                            ExternalSourceProgress value =
                                DeserializeProgress(payload);

                            if (value != null)
                                progress.Report(value);

                            lastPayload = payload;
                        }
                    }
                }
                catch (IOException)
                {
                    // The worker may be replacing the sidecar file.
                }
                catch (UnauthorizedAccessException)
                {
                    // Progress is best-effort; the response remains authoritative.
                }
                catch (SerializationException)
                {
                    // Retry if a replacement record was observed mid-write.
                }
                catch (Exception exception)
                {
                    Debug.WriteLine(
                        "[PHASE72] Progress monitor read failed: " +
                        exception);
                }

                if (state.StopRequested)
                    break;

                // No cancellation token is used. Normal monitor shutdown must
                // not create TaskCanceledException as part of control flow.
                await Task.Delay(ProgressPollMilliseconds)
                    .ConfigureAwait(false);
            }
        }

        private static string ReadProgressPayload(
            string progressPath)
        {
            using (FileStream stream = File.Open(
                progressPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }

        private static ExternalSourceProgress DeserializeProgress(
            string payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
                return null;

            byte[] bytes = Encoding.UTF8.GetBytes(payload);

            using (var stream = new MemoryStream(bytes))
            {
                var serializer =
                    new DataContractJsonSerializer(
                        typeof(ExternalSourceProgress));

                return (ExternalSourceProgress)
                    serializer.ReadObject(stream);
            }
        }

        private static void ReportFinalProgressIfAvailable(
            string progressPath,
            IProgress<ExternalSourceProgress> progress)
        {
            if (progress == null || !File.Exists(progressPath))
                return;

            try
            {
                string payload =
                    ReadProgressPayload(progressPath);

                ExternalSourceProgress value =
                    DeserializeProgress(payload);

                if (value != null)
                    progress.Report(value);
            }
            catch (IOException)
            {
                // Final response remains authoritative.
            }
            catch (UnauthorizedAccessException)
            {
                // Best-effort progress only.
            }
            catch (SerializationException)
            {
                // Best-effort progress only.
            }
            catch (Exception exception)
            {
                Debug.WriteLine(
                    "[PHASE72] Final progress read failed: " +
                    exception);
            }
        }

        private static void StopMonitorSafely(
            Task monitorTask)
        {
            if (monitorTask == null)
                return;

            try
            {
                bool completed = monitorTask.Wait(
                    MonitorShutdownWaitMilliseconds);

                if (!completed)
                {
                    Debug.WriteLine(
                        "[PHASE72] Progress monitor did not stop " +
                        "within the bounded shutdown wait.");
                }
            }
            catch (AggregateException exception)
            {
                Debug.WriteLine(
                    "[PHASE72] Progress monitor shutdown error: " +
                    exception.Flatten());
            }
            catch (Exception exception)
            {
                Debug.WriteLine(
                    "[PHASE72] Progress monitor shutdown error: " +
                    exception);
            }
        }

        private static void Report(
            IProgress<ExternalSourceProgress> progress,
            string stage,
            int current,
            int total,
            string message,
            bool determinate)
        {
            if (progress == null)
                return;

            progress.Report(
                new ExternalSourceProgress
                {
                    Stage = stage,
                    Current = current,
                    Total = total,
                    Message = message,
                    IsDeterminate = determinate
                });
        }

        private static void TryDelete(
            string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception exception)
            {
                Debug.WriteLine(
                    "[PHASE72] Temporary-file cleanup failed for '" +
                    path + "': " + exception);
            }
        }
    }
}
