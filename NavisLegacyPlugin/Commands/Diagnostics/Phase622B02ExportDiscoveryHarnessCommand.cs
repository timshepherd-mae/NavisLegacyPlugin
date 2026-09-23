using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Automation;
using Autodesk.Navisworks.Api.Plugins;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace NavisLegacyPlugin.Commands.Diagnostics
{
    [Plugin(
        "Phase622B02ExportDiscoveryHarness",
        "MAE",
        DisplayName = "P622B-02 Export Discovery Feasibility",
        ToolTip = "Test EXPORT discovery through a separate Automation-owned Navisworks session")]
    public sealed class Phase622B02ExportDiscoveryHarnessCommand : AddInPlugin
    {
        private const string WorkerMode = "WORKER";
        private const string PluginId = "Phase622B02ExportDiscoveryHarness.MAE";
        private static readonly string[] RequiredPath =
        {
            "MAE-4D", "DATA-TRANSFER", "EXPORT"
        };

        public override int Execute(params string[] parameters)
        {
            if (parameters != null &&
                parameters.Length > 0 &&
                string.Equals(parameters[0], WorkerMode, StringComparison.Ordinal))
            {
                return ExecuteWorker(parameters);
            }

            return ExecuteController();
        }

        private static int ExecuteController()
        {
            string selectedPath = SelectSourceFile();
            if (string.IsNullOrWhiteSpace(selectedPath)) return 0;

            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string controllerReport = Path.Combine(
                Path.GetTempPath(),
                "NavisLegacy_P622B02_Controller_" + stamp + ".txt");
            string workerReport = Path.Combine(
                Path.GetTempPath(),
                "NavisLegacy_P622B02_Worker_" + stamp + ".txt");

            Document activeBefore = Autodesk.Navisworks.Api.Application.ActiveDocument;
            string activeIdentityBefore = GetDocumentIdentity(activeBefore);
            List<Guid> selectionBefore = GetSelectionIdentity(activeBefore);
            NavisworksApplication automation = null;
            bool created = false;
            bool opened = false;
            bool workerReturned = false;
            bool disposed = false;

            using (StreamWriter log = CreateLog(controllerReport))
            {
                Write(log, "P622B-02 CONTROLLER START");
                Write(log, "Controller process ID: " + Process.GetCurrentProcess().Id);
                Write(log, "External source: " + selectedPath);
                Write(log, "Worker report: " + workerReport);
                Write(log, "Active document before: " + activeIdentityBefore);
                Write(log, "Active selection count before: " + selectionBefore.Count);

                try
                {
                    Write(log, "Constructing NavisworksApplication");
                    automation = new NavisworksApplication();
                    created = true;
                    Write(log, "NavisworksApplication constructor returned");

                    Write(log, "Calling Automation.OpenFile");
                    automation.OpenFile(selectedPath);
                    opened = true;
                    Write(log, "Automation.OpenFile returned");

                    Write(log, "Calling Automation.ExecuteAddInPlugin: " + PluginId);
                    automation.ExecuteAddInPlugin(
                        PluginId,
                        WorkerMode,
                        workerReport,
                        selectedPath);
                    workerReturned = true;
                    Write(log, "Automation.ExecuteAddInPlugin returned");
                }
                catch (Exception exception)
                {
                    Write(log, "CONTROLLER EXCEPTION: " + FormatException(exception));
                }
                finally
                {
                    if (automation != null)
                    {
                        Write(log, "Calling NavisworksApplication.Dispose");
                        try
                        {
                            automation.Dispose();
                            disposed = true;
                            Write(log, "NavisworksApplication.Dispose returned");
                        }
                        catch (Exception exception)
                        {
                            Write(log, "DISPOSE EXCEPTION: " + FormatException(exception));
                        }
                    }
                }

                Document activeAfter = Autodesk.Navisworks.Api.Application.ActiveDocument;
                string activeIdentityAfter = GetDocumentIdentity(activeAfter);
                List<Guid> selectionAfter = GetSelectionIdentity(activeAfter);
                bool documentPreserved =
                    object.ReferenceEquals(activeBefore, activeAfter) &&
                    string.Equals(activeIdentityBefore, activeIdentityAfter, StringComparison.Ordinal);
                bool selectionPreserved = selectionBefore.SequenceEqual(selectionAfter);

                Write(log, "Active document after: " + activeIdentityAfter);
                Write(log, "Active document preserved: " + documentPreserved);
                Write(log, "Active selection preserved: " + selectionPreserved);
                Write(log, "Automation created: " + created);
                Write(log, "Automation open returned: " + opened);
                Write(log, "Worker invocation returned: " + workerReturned);
                Write(log, "Automation dispose returned: " + disposed);
                Write(log, "Worker report exists: " + File.Exists(workerReport));
                Write(log, "P622B-02 CONTROLLER MANAGED CODE COMPLETE");
            }

            return created && opened && workerReturned && disposed ? 0 : -1;
        }

        private static int ExecuteWorker(string[] parameters)
        {
            string reportPath = parameters.Length > 1 ? parameters[1] : null;
            string expectedSource = parameters.Length > 2 ? parameters[2] : "<not supplied>";
            if (string.IsNullOrWhiteSpace(reportPath)) return -1;

            using (StreamWriter log = CreateLog(reportPath))
            {
                Write(log, "P622B-02 WORKER START");
                Write(log, "Worker process ID: " + Process.GetCurrentProcess().Id);
                Write(log, "Expected source: " + expectedSource);
                try
                {
                    Document document = Autodesk.Navisworks.Api.Application.ActiveDocument;
                    Write(log, "Worker ActiveDocument: " + GetDocumentIdentity(document));
                    if (document == null)
                    {
                        throw new InvalidOperationException("Automation worker has no ActiveDocument.");
                    }

                    GroupItem exportFolder = FindRequiredFolder(document, RequiredPath);
                    List<SavedItem> children = exportFolder.Children.Cast<SavedItem>().ToList();
                    Write(log, "EXPORT direct child count: " + children.Count);
                    if (children.Count == 0)
                    {
                        throw new InvalidOperationException(
                            "Selection Set folder 'MAE-4D/DATA-TRANSFER/EXPORT' contains no export sets.");
                    }

                    List<SavedItem> invalid = children.Where(x => !(x is SelectionSet)).ToList();
                    if (invalid.Count > 0)
                    {
                        throw new InvalidOperationException(
                            "EXPORT contains non-SelectionSet direct child item(s): " +
                            string.Join(", ", invalid.Select(x => x.DisplayName).ToArray()));
                    }

                    List<SelectionSet> sets = children
                        .OfType<SelectionSet>()
                        .OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    List<string> duplicates = sets
                        .GroupBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
                        .Where(x => x.Count() > 1)
                        .Select(x => x.Key)
                        .ToList();
                    if (duplicates.Count > 0)
                    {
                        throw new InvalidOperationException(
                            "Duplicate export set name(s): " + string.Join(", ", duplicates.ToArray()));
                    }

                    for (int index = 0; index < sets.Count; index++)
                    {
                        string name = sets[index].DisplayName;
                        Write(log, string.Format(
                            "EXPORT SET {0}: {1} | MAE-4D/DATA-TRANSFER/EXPORT/{1}",
                            index + 1,
                            name));
                    }

                    Write(log, "No SelectionSet.GetSelectedItems call was performed.");
                    Write(log, "P622B-02 RESULT: PASS");
                    Write(log, "P622B-02 WORKER MANAGED CODE COMPLETE");
                    return 0;
                }
                catch (Exception exception)
                {
                    Write(log, "P622B-02 RESULT: FAIL");
                    Write(log, "WORKER EXCEPTION: " + FormatException(exception));
                    Write(log, "P622B-02 WORKER MANAGED CODE COMPLETE");
                    return -1;
                }
            }
        }

        private static GroupItem FindRequiredFolder(Document document, IEnumerable<string> path)
        {
            SavedItemCollection children = document.SelectionSets.RootItem.Children;
            GroupItem current = null;
            foreach (string part in path)
            {
                List<SavedItem> matches = children.Cast<SavedItem>()
                    .Where(x => string.Equals(x.DisplayName, part, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (matches.Count == 0)
                {
                    throw new InvalidOperationException(
                        "Required Selection Set folder '" + part + "' was not found.");
                }
                if (matches.Count > 1)
                {
                    throw new InvalidOperationException(
                        "Required Selection Set item '" + part + "' is ambiguous.");
                }
                current = matches[0] as GroupItem;
                if (current == null)
                {
                    throw new InvalidOperationException(
                        "Required Selection Set item '" + part + "' is not a folder.");
                }
                children = current.Children;
            }
            return current;
        }

        private static StreamWriter CreateLog(string path)
        {
            StreamWriter log = new StreamWriter(path, false, new UTF8Encoding(true));
            log.AutoFlush = true;
            return log;
        }

        private static void Write(StreamWriter log, string value)
        {
            log.WriteLine(DateTime.UtcNow.ToString("O") + " | " + value);
        }

        private static string SelectSourceFile()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Select external NWD/NWF with an EXPORT folder";
                dialog.Filter = "Navisworks files (*.nwd;*.nwf)|*.nwd;*.nwf";
                dialog.CheckFileExists = true;
                dialog.Multiselect = false;
                return dialog.ShowDialog() == DialogResult.OK ? dialog.FileName : null;
            }
        }

        private static string FormatException(Exception exception)
        {
            StringBuilder value = new StringBuilder();
            Exception current = exception;
            while (current != null)
            {
                if (value.Length > 0) value.Append(" --> ");
                value.Append(current.GetType().FullName);
                value.Append(": ");
                value.Append(current.Message);
                current = current.InnerException;
            }
            return value.ToString();
        }

        private static string GetDocumentIdentity(Document document)
        {
            if (document == null) return "<null>";
            return string.IsNullOrWhiteSpace(document.FileName) ? "<unsaved>" : document.FileName;
        }

        private static List<Guid> GetSelectionIdentity(Document document)
        {
            if (document == null) return new List<Guid>();
            return document.CurrentSelection.SelectedItems
                .Cast<ModelItem>()
                .Select(x => x.InstanceGuid)
                .ToList();
        }
    }
}
