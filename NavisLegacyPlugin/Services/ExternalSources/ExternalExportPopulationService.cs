using Autodesk.Navisworks.Api.Automation;
using NavisLegacyPlugin.Models.ExternalSources;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
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
                "NavisLegacy_Phase72_" + token + ".progress");

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
                TryDeleteProgressRecords(progressPath);
            }
        }

        private static async Task MonitorProgressAsync(
            string progressPath,
            IProgress<ExternalSourceProgress> progress,
            ProgressMonitorState state)
        {
            if (progress == null || state == null)
                return;

            var processedRecords = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

            while (!state.StopRequested)
            {
                ReportNewProgressRecords(
                    progressPath,
                    progress,
                    processedRecords);

                if (state.StopRequested)
                    break;

                await Task.Delay(ProgressPollMilliseconds)
                    .ConfigureAwait(false);
            }

            ReportNewProgressRecords(
                progressPath,
                progress,
                processedRecords);
        }

        private static void ReportNewProgressRecords(
            string progressPath,
            IProgress<ExternalSourceProgress> progress,
            ISet<string> processedRecords)
        {
            string[] records = GetProgressRecordPaths(progressPath);

            foreach (string recordPath in records)
            {
                if (processedRecords.Contains(recordPath))
                    continue;

                ExternalSourceProgress value =
                    TryReadProgressRecord(recordPath);

                if (value == null)
                    continue;

                progress.Report(value);
                processedRecords.Add(recordPath);
            }
        }

        private static ExternalSourceProgress TryReadProgressRecord(
            string recordPath)
        {
            try
            {
                using (FileStream stream = new FileStream(
                    recordPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read | FileShare.Delete))
                {
                    var serializer =
                        new DataContractJsonSerializer(
                            typeof(ExternalSourceProgress));

                    return (ExternalSourceProgress)
                        serializer.ReadObject(stream);
                }
            }
            catch (FileNotFoundException)
            {
                return null;
            }
            catch (DirectoryNotFoundException)
            {
                return null;
            }
            catch (SerializationException)
            {
                return null;
            }
        }

        private static void ReportFinalProgressIfAvailable(
            string progressPath,
            IProgress<ExternalSourceProgress> progress)
        {
            if (progress == null)
                return;

            string[] records = GetProgressRecordPaths(progressPath);
            if (records.Length == 0)
                return;

            ExternalSourceProgress value =
                TryReadProgressRecord(records[records.Length - 1]);

            if (value != null)
                progress.Report(value);
        }

        private static string[] GetProgressRecordPaths(
            string progressPath)
        {
            string directory = Path.GetDirectoryName(progressPath);
            string prefix = Path.GetFileName(progressPath) + ".";

            if (string.IsNullOrWhiteSpace(directory) ||
                !Directory.Exists(directory))
            {
                return new string[0];
            }

            try
            {
                return Directory
                    .GetFiles(directory, prefix + "*.record")
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToArray();
            }
            catch (DirectoryNotFoundException)
            {
                return new string[0];
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

        private static void TryDeleteProgressRecords(
            string progressPath)
        {
            foreach (string recordPath in GetProgressRecordPaths(progressPath))
                TryDelete(recordPath);
        }

        private static void TryDelete(string path)
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
