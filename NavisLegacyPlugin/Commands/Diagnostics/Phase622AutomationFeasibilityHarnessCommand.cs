using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Automation;
using Autodesk.Navisworks.Api.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace NavisLegacyPlugin.Commands.Diagnostics
{
    [Plugin(
        "Phase622AutomationFeasibilityHarness",
        "MAE",
        DisplayName = "Phase 6.2.2 Automation Feasibility Test",
        ToolTip = "Test a single Navisworks Automation open/dispose cycle")]
    public sealed class Phase622AutomationFeasibilityHarnessCommand : AddInPlugin
    {
        public override int Execute(params string[] parameters)
        {
            string selectedPath = SelectSourceFile();
            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                return 0;
            }

            string reportPath = Path.Combine(
                Path.GetTempPath(),
                "NavisLegacy_Phase6_2_2_AutomationFeasibility_" +
                DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");

            Document activeBefore = Autodesk.Navisworks.Api.Application.ActiveDocument;
            string activeIdentityBefore = GetDocumentIdentity(activeBefore);
            List<Guid> selectionBefore = GetSelectionIdentity(activeBefore);
            bool created = false;
            bool opened = false;
            bool disposed = false;

            using (StreamWriter log = new StreamWriter(
                reportPath,
                false,
                new UTF8Encoding(true)))
            {
                log.AutoFlush = true;
                Write(log, "HARNESS START");
                Write(log, "Purpose: one Automation create/open/dispose cycle only");
                Write(log, "Selected external source: " + selectedPath);
                Write(log, "Active document before: " + activeIdentityBefore);
                Write(log, "Active selection count before: " + selectionBefore.Count);
                Write(log, "Current process ID: " + System.Diagnostics.Process.GetCurrentProcess().Id);

                NavisworksApplication automation = null;
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
                }
                catch (Exception exception)
                {
                    Write(log, "MANAGED EXCEPTION BEFORE DISPOSE: " + FormatException(exception));
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
                            Write(log, "MANAGED EXCEPTION DURING DISPOSE: " + FormatException(exception));
                        }
                    }
                    else
                    {
                        Write(log, "Automation object was not created; Dispose not called");
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
                Write(log, "Active selection count after: " + selectionAfter.Count);
                Write(log, "Active document preserved: " + documentPreserved);
                Write(log, "Active selection preserved: " + selectionPreserved);
                Write(log, "Automation created: " + created);
                Write(log, "Automation open returned: " + opened);
                Write(log, "Automation dispose returned: " + disposed);
                Write(log, "HARNESS MANAGED CODE COMPLETE");
                Write(log, "No EXPORT discovery, SelectionSet access, ModelItem extraction, transfer, repetition, or final dialog was performed.");
            }

            // Deliberately no final MessageBox: avoid adding a second message loop/lifecycle variable.
            return created && opened && disposed ? 0 : -1;
        }

        private static string SelectSourceFile()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Select one NWD or NWF for the Automation feasibility test";
                dialog.Filter = "Navisworks files (*.nwd;*.nwf)|*.nwd;*.nwf";
                dialog.CheckFileExists = true;
                dialog.Multiselect = false;
                return dialog.ShowDialog() == DialogResult.OK ? dialog.FileName : null;
            }
        }

        private static void Write(StreamWriter log, string message)
        {
            log.WriteLine(DateTime.UtcNow.ToString("O") + " | " + message);
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
