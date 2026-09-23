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
    [Plugin("Phase622B03PropertyExtractionHarness", "MAE",
        DisplayName = "P622B-03 ModelItem Property Extraction Feasibility",
        ToolTip = "Test selected EXPORT-set ModelItem/property reading in an Automation-owned session")]
    public sealed class Phase622B03PropertyExtractionHarnessCommand : AddInPlugin
    {
        private const string WorkerMode = "WORKER";
        private const string PluginId = "Phase622B03PropertyExtractionHarness.MAE";
        private const int MaximumSampleItems = 3;
        private const int MaximumPropertiesPerItem = 20;
        private static readonly string[] ExportFolderPath = { "MAE-4D", "DATA-TRANSFER", "EXPORT" };

        public override int Execute(params string[] parameters)
        {
            if (parameters != null && parameters.Length > 0 &&
                string.Equals(parameters[0], WorkerMode, StringComparison.Ordinal))
                return ExecuteWorker(parameters);
            return ExecuteController();
        }

        private static int ExecuteController()
        {
            string source = SelectSourceFile();
            if (string.IsNullOrWhiteSpace(source)) return 0;
            string exportName = PromptForExportSetName();
            if (string.IsNullOrWhiteSpace(exportName)) return 0;

            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string controllerReport = Path.Combine(Path.GetTempPath(), "NavisLegacy_P622B03_Controller_" + stamp + ".txt");
            string workerReport = Path.Combine(Path.GetTempPath(), "NavisLegacy_P622B03_Worker_" + stamp + ".txt");
            Document before = Autodesk.Navisworks.Api.Application.ActiveDocument;
            string beforeId = GetDocumentIdentity(before);
            List<Guid> beforeSelection = GetSelectionIdentity(before);
            NavisworksApplication automation = null;
            bool created = false, opened = false, workerReturned = false, disposed = false;

            using (StreamWriter log = CreateLog(controllerReport))
            {
                Write(log, "P622B-03 CONTROLLER START");
                Write(log, "Controller process ID: " + Process.GetCurrentProcess().Id);
                Write(log, "External source: " + source);
                Write(log, "Requested export set: " + exportName.Trim());
                Write(log, "Worker report: " + workerReport);
                Write(log, "Active document before: " + beforeId);
                Write(log, "Active selection count before: " + beforeSelection.Count);
                try
                {
                    Write(log, "Constructing NavisworksApplication");
                    automation = new NavisworksApplication(); created = true;
                    Write(log, "NavisworksApplication constructor returned");
                    automation.OpenFile(source); opened = true;
                    Write(log, "Automation.OpenFile returned");
                    automation.ExecuteAddInPlugin(PluginId, WorkerMode, workerReport, source, exportName.Trim());
                    workerReturned = true;
                    Write(log, "Automation.ExecuteAddInPlugin returned");
                }
                catch (Exception ex) { Write(log, "CONTROLLER EXCEPTION: " + FormatException(ex)); }
                finally
                {
                    if (automation != null)
                    {
                        Write(log, "Calling NavisworksApplication.Dispose");
                        try { automation.Dispose(); disposed = true; Write(log, "NavisworksApplication.Dispose returned"); }
                        catch (Exception ex) { Write(log, "DISPOSE EXCEPTION: " + FormatException(ex)); }
                    }
                }
                Document after = Autodesk.Navisworks.Api.Application.ActiveDocument;
                List<Guid> afterSelection = GetSelectionIdentity(after);
                bool documentPreserved = object.ReferenceEquals(before, after) &&
                    string.Equals(beforeId, GetDocumentIdentity(after), StringComparison.Ordinal);
                Write(log, "Active document preserved: " + documentPreserved);
                Write(log, "Active selection preserved: " + beforeSelection.SequenceEqual(afterSelection));
                Write(log, "Automation created: " + created);
                Write(log, "Automation open returned: " + opened);
                Write(log, "Worker invocation returned: " + workerReturned);
                Write(log, "Automation dispose returned: " + disposed);
                Write(log, "Worker report exists: " + File.Exists(workerReport));
                Write(log, "P622B-03 CONTROLLER MANAGED CODE COMPLETE");
            }
            return created && opened && workerReturned && disposed ? 0 : -1;
        }

        private static int ExecuteWorker(string[] parameters)
        {
            string report = parameters.Length > 1 ? parameters[1] : null;
            string expectedSource = parameters.Length > 2 ? parameters[2] : "<not supplied>";
            string exportName = parameters.Length > 3 ? parameters[3] : null;
            if (string.IsNullOrWhiteSpace(report) || string.IsNullOrWhiteSpace(exportName)) return -1;
            using (StreamWriter log = CreateLog(report))
            {
                Write(log, "P622B-03 WORKER START");
                Write(log, "Worker process ID: " + Process.GetCurrentProcess().Id);
                Write(log, "Expected source: " + expectedSource);
                Write(log, "Requested export set: " + exportName);
                try
                {
                    Document document = Autodesk.Navisworks.Api.Application.ActiveDocument;
                    Write(log, "Worker ActiveDocument: " + GetDocumentIdentity(document));
                    if (document == null) throw new InvalidOperationException("Automation worker has no ActiveDocument.");
                    GroupItem folder = FindRequiredFolder(document, ExportFolderPath);
                    List<SelectionSet> matches = folder.Children.Cast<SavedItem>().OfType<SelectionSet>()
                        .Where(x => string.Equals(x.DisplayName, exportName, StringComparison.OrdinalIgnoreCase)).ToList();
                    if (matches.Count == 0) throw new InvalidOperationException("Export Selection Set was not found: " + exportName);
                    if (matches.Count > 1) throw new InvalidOperationException("Export Selection Set name is ambiguous: " + exportName);

                    ModelItemCollection items = matches[0].GetSelectedItems();
                    Write(log, "SelectionSet.GetSelectedItems returned");
                    Write(log, "Selected ModelItem count: " + items.Count);
                    if (items.Count == 0) throw new InvalidOperationException("Selected export set contains no ModelItems.");

                    int itemIndex = 0;
                    foreach (ModelItem item in items.Take(MaximumSampleItems))
                    {
                        itemIndex++;
                        Write(log, "ITEM " + itemIndex + " DisplayName: " + SafeText(item.DisplayName));
                        Write(log, "ITEM " + itemIndex + " InstanceGuid: " + item.InstanceGuid);
                        int propertyIndex = 0;
                        foreach (PropertyCategory category in item.PropertyCategories)
                        {
                            foreach (DataProperty property in category.Properties)
                            {
                                propertyIndex++;
                                string value;
                                try { value = property.Value == null ? "<null>" : property.Value.ToDisplayString(); }
                                catch (Exception ex) { value = "<value read failed: " + ex.GetType().Name + ">"; }
                                Write(log, string.Format("ITEM {0} PROPERTY {1}: {2} / {3} = {4}",
                                    itemIndex, propertyIndex, SafeText(category.DisplayName),
                                    SafeText(property.DisplayName), SafeText(value)));
                                if (propertyIndex >= MaximumPropertiesPerItem) break;
                            }
                            if (propertyIndex >= MaximumPropertiesPerItem) break;
                        }
                        Write(log, "ITEM " + itemIndex + " sampled property count: " + propertyIndex);
                    }
                    Write(log, "Sampled ModelItem count: " + Math.Min(items.Count, MaximumSampleItems));
                    Write(log, "No Collection Resolver, transfer, or write operation was performed.");
                    Write(log, "P622B-03 RESULT: PASS");
                    Write(log, "P622B-03 WORKER MANAGED CODE COMPLETE");
                    return 0;
                }
                catch (Exception ex)
                {
                    Write(log, "P622B-03 RESULT: FAIL");
                    Write(log, "WORKER EXCEPTION: " + FormatException(ex));
                    Write(log, "P622B-03 WORKER MANAGED CODE COMPLETE");
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
                    .Where(x => string.Equals(x.DisplayName, part, StringComparison.OrdinalIgnoreCase)).ToList();
                if (matches.Count != 1) throw new InvalidOperationException("Required Selection Set folder is missing or ambiguous: " + part);
                current = matches[0] as GroupItem;
                if (current == null) throw new InvalidOperationException("Required Selection Set item is not a folder: " + part);
                children = current.Children;
            }
            return current;
        }

        private static string SelectSourceFile()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Select external NWD/NWF containing the required EXPORT set";
                dialog.Filter = "Navisworks files (*.nwd;*.nwf)|*.nwd;*.nwf";
                dialog.CheckFileExists = true;
                return dialog.ShowDialog() == DialogResult.OK ? dialog.FileName : null;
            }
        }

        private static string PromptForExportSetName()
        {
            using (Form form = new Form())
            using (TextBox input = new TextBox())
            using (Button ok = new Button())
            using (Button cancel = new Button())
            {
                form.Text = "P622B-03 Export Set"; form.Width = 420; form.Height = 145;
                form.StartPosition = FormStartPosition.CenterScreen; form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MinimizeBox = false; form.MaximizeBox = false;
                input.Left = 12; input.Top = 12; input.Width = 378;
                ok.Text = "OK"; ok.Left = 234; ok.Top = 48; ok.DialogResult = DialogResult.OK;
                cancel.Text = "Cancel"; cancel.Left = 315; cancel.Top = 48; cancel.DialogResult = DialogResult.Cancel;
                form.Controls.Add(input); form.Controls.Add(ok); form.Controls.Add(cancel);
                form.AcceptButton = ok; form.CancelButton = cancel;
                return form.ShowDialog() == DialogResult.OK ? input.Text : null;
            }
        }

        private static StreamWriter CreateLog(string path)
        { StreamWriter writer = new StreamWriter(path, false, new UTF8Encoding(true)); writer.AutoFlush = true; return writer; }
        private static void Write(StreamWriter log, string value)
        { log.WriteLine(DateTime.UtcNow.ToString("O") + " | " + value); }
        private static string SafeText(string value)
        { return string.IsNullOrEmpty(value) ? "<empty>" : value.Replace("\r", " ").Replace("\n", " "); }
        private static string FormatException(Exception ex)
        {
            StringBuilder value = new StringBuilder();
            while (ex != null) { if (value.Length > 0) value.Append(" --> "); value.Append(ex.GetType().FullName).Append(": ").Append(ex.Message); ex = ex.InnerException; }
            return value.ToString();
        }
        private static string GetDocumentIdentity(Document document)
        { return document == null ? "<null>" : (string.IsNullOrWhiteSpace(document.FileName) ? "<unsaved>" : document.FileName); }
        private static List<Guid> GetSelectionIdentity(Document document)
        { return document == null ? new List<Guid>() : document.CurrentSelection.SelectedItems.Cast<ModelItem>().Select(x => x.InstanceGuid).ToList(); }
    }
}
