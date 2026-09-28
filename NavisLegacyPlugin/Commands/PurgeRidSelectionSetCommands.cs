using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Plugins;
using ComApi = Autodesk.Navisworks.Api.Interop.ComApi;
using ComApiBridge = Autodesk.Navisworks.Api.ComApi;

namespace NavisLegacyPlugin.Commands
{
    internal static class PurgeRidConstants
    {
        public const string CandidateSetName = "RID-PURGE-CANDIDATES";
        public const string TabName = "MAE-4D";
        public const string PropertyName = "RID";
    }

    internal static class PurgeRidDiagnosticLogger
    {
        private static readonly object SyncRoot = new object();
        private static string _filePath;

        public static string Start()
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "NavisLegacy", "Diagnostics");
            Directory.CreateDirectory(folder);
            _filePath = Path.Combine(folder,
                "PurgeRid_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".log");
            Write("Diagnostic logging started.");
            return _filePath;
        }

        public static void Write(string message)
        {
            if (string.IsNullOrEmpty(_filePath)) return;
            lock (SyncRoot)
            {
                File.AppendAllText(_filePath,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) +
                    " | " + message + Environment.NewLine, Encoding.UTF8);
            }
        }

        public static string FilePath { get { return _filePath ?? string.Empty; } }
    }

    [Plugin("FindBlankRidCandidates", "MAE", DisplayName = "Find Blank RID Candidates",
        ToolTip = "Create or refresh the RID-PURGE-CANDIDATES Selection Set")]
    public sealed class FindBlankRidCandidatesCommand : AddInPlugin
    {
        public override int Execute(params string[] parameters)
        {
            Document document = Autodesk.Navisworks.Api.Application.ActiveDocument;
            if (document == null || document.Models == null || document.Models.Count == 0)
            {
                MessageBox.Show("There is no active model to inspect.", "Find Blank RID Candidates");
                return 0;
            }

            try
            {
                string logPath = PurgeRidDiagnosticLogger.Start();
                var candidates = new ModelItemCollection();
                foreach (ModelItem root in document.Models.RootItems)
                    AddCandidates(root, candidates);

                var candidateSet = new SelectionSet(candidates);
                candidateSet.DisplayName = PurgeRidConstants.CandidateSetName;
                int existingIndex = FindRootSetIndex(document, PurgeRidConstants.CandidateSetName);
                if (existingIndex >= 0)
                    document.SelectionSets.ReplaceWithCopy(existingIndex, candidateSet);
                else
                    document.SelectionSets.AddCopy(candidateSet);

                PurgeRidDiagnosticLogger.Write("Candidate scan complete. Count=" + candidates.Count);
                MessageBox.Show("Selection Set created/refreshed: " + PurgeRidConstants.CandidateSetName +
                    Environment.NewLine + "Candidates: " + candidates.Count +
                    Environment.NewLine + "Diagnostic log: " + logPath,
                    "Find Blank RID Candidates", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 0;
            }
            catch (Exception ex)
            {
                PurgeRidDiagnosticLogger.Write("ERROR: " + ex);
                MessageBox.Show("Candidate search failed:" + Environment.NewLine + ex.Message +
                    Environment.NewLine + "Diagnostic log: " + PurgeRidDiagnosticLogger.FilePath,
                    "Find Blank RID Candidates", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return -1;
            }
        }

        private static void AddCandidates(ModelItem item, ModelItemCollection output)
        {
            if (PurgeRidPropertyUtility.HasBlankRid(item)) output.Add(item);
            foreach (ModelItem child in item.Children) AddCandidates(child, output);
        }

        private static int FindRootSetIndex(Document document, string name)
        {
            for (int i = 0; i < document.SelectionSets.RootItem.Children.Count; i++)
            {
                SavedItem item = document.SelectionSets.RootItem.Children[i];
                if (item is SelectionSet && string.Equals(item.DisplayName, name, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return -1;
        }
    }

    [Plugin("PurgeBlankRid", "MAE", DisplayName = "Purge Blank RID",
        ToolTip = "Remove blank RID only from items in RID-PURGE-CANDIDATES")]
    public sealed class PurgeBlankRidCommand : AddInPlugin
    {
        public override int Execute(params string[] parameters)
        {
            Document document = Autodesk.Navisworks.Api.Application.ActiveDocument;
            if (document == null) return 0;
            if (string.IsNullOrEmpty(PurgeRidDiagnosticLogger.FilePath)) PurgeRidDiagnosticLogger.Start();
            
            SelectionSet set = FindRootSelectionSet(document, PurgeRidConstants.CandidateSetName);
            if (set == null)
            {
                MessageBox.Show("Run Find Blank RID Candidates first.", "Purge Blank RID");
                return 0;
            }

            ModelItemCollection items = set.GetSelectedItems();
            int eligible = 0;
            foreach (ModelItem item in items)
                if (PurgeRidPropertyUtility.HasBlankRid(item)) eligible++;

            if (MessageBox.Show("Current blank RID candidates: " + eligible + Environment.NewLine +
                "Remove those RID properties now?", "Purge Blank RID",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return 0;

            int changed = 0;
            int removed = 0;
            foreach (ModelItem item in items)
            {
                if (!PurgeRidPropertyUtility.HasBlankRid(item)) continue;
                int count;
                if (PurgeRidPropertyUtility.Purge(item, out count)) { changed++; removed += count; }
            }

            PurgeRidDiagnosticLogger.Write("Purge complete. ItemsChanged=" + changed + "; PropertiesRemoved=" + removed);
            MessageBox.Show("ModelItems changed: " + changed + Environment.NewLine +
                "RID properties removed: " + removed + Environment.NewLine +
                "Diagnostic log: " + PurgeRidDiagnosticLogger.FilePath,
                "Purge Blank RID", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 0;
        }

        private static SelectionSet FindRootSelectionSet(Document document, string name)
        {
            foreach (SavedItem item in document.SelectionSets.RootItem.Children)
            {
                SelectionSet set = item as SelectionSet;
                if (set != null && string.Equals(set.DisplayName, name, StringComparison.OrdinalIgnoreCase)) return set;
            }
            return null;
        }
    }

    internal static class PurgeRidPropertyUtility
    {
        // Complete replacement: inspect the underlying user-defined COM category/property,
        // log the raw type/value, and use exactly the same matching rules as Purge().
        public static bool HasBlankRid(ModelItem item)
        {
            ComApi.InwOaPath path = null;
            ComApi.InwGUIPropertyNode2 node = null;
            try
            {
                ComApi.InwOpState10 state = ComApiBridge.ComApiBridge.State;
                path = ComApiBridge.ComApiBridge.ToInwOaPath(item);
                node = (ComApi.InwGUIPropertyNode2)state.GetGUIPropertyNode(path, true);

                foreach (ComApi.InwGUIAttribute2 attribute in (IEnumerable)node.GUIAttributes())
                {
                    if (attribute == null || !attribute.UserDefined) continue;
                    if (!string.Equals(Convert.ToString(attribute.ClassUserName), PurgeRidConstants.TabName,
                        StringComparison.OrdinalIgnoreCase)) continue;

                    foreach (ComApi.InwOaProperty property in (IEnumerable)attribute.Properties())
                    {
                        if (property == null) continue;
                        if (!string.Equals(Convert.ToString(property.UserName), PurgeRidConstants.PropertyName,
                            StringComparison.OrdinalIgnoreCase)) continue;

                        object raw = property.value;
                        string text = raw == null ? null : Convert.ToString(raw, CultureInfo.InvariantCulture);
                        bool candidate = IsPurgeValue(text);
                        PurgeRidDiagnosticLogger.Write(
                            "RID inspected | Item=" + SafeItemName(item) +
                            " | RawType=" + (raw == null ? "<null>" : raw.GetType().FullName) +
                            " | Length=" + (text == null ? "<null>" : text.Length.ToString(CultureInfo.InvariantCulture)) +
                            " | Value=" + VisibleValue(text) +
                            " | Candidate=" + candidate);
                        if (candidate) return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                PurgeRidDiagnosticLogger.Write("HasBlankRid ERROR | Item=" + SafeItemName(item) + " | " + ex);
                return false;
            }
            finally
            {
                Release(node);
                Release(path);
            }
        }

        private static bool IsPurgeValue(string text)
        {
            return text == null || string.IsNullOrWhiteSpace(text) || text == "\"\"";
        }

        public static bool Purge(ModelItem item, out int removedCount)
        {
            removedCount = 0;
            ComApi.InwOaPath path = null;
            ComApi.InwGUIPropertyNode2 node = null;
            try
            {
                ComApi.InwOpState10 state = ComApiBridge.ComApiBridge.State;
                path = ComApiBridge.ComApiBridge.ToInwOaPath(item);
                node = (ComApi.InwGUIPropertyNode2)state.GetGUIPropertyNode(path, true);
                int userDefinedIndex = 1;
                foreach (ComApi.InwGUIAttribute2 attribute in (IEnumerable)node.GUIAttributes())
                {
                    if (attribute == null || !attribute.UserDefined) continue;
                    if (string.Equals(Convert.ToString(attribute.ClassUserName), PurgeRidConstants.TabName, StringComparison.OrdinalIgnoreCase))
                    {
                        int removed;
                        ComApi.InwOaPropertyVec replacement = BuildReplacement(state, attribute, out removed);
                        try
                        {
                            if (removed > 0)
                            {
                                node.SetUserDefined(userDefinedIndex, attribute.ClassUserName, attribute.ClassName, replacement);
                                removedCount += removed;
                            }
                        }
                        finally { Release(replacement); }
                    }
                    userDefinedIndex++;
                }
                return removedCount > 0;
            }
            finally { Release(node); Release(path); }
        }

        private static ComApi.InwOaPropertyVec BuildReplacement(ComApi.InwOpState10 state,
            ComApi.InwGUIAttribute2 attribute, out int removed)
        {
            removed = 0;
            var vector = (ComApi.InwOaPropertyVec)state.ObjectFactory(
                ComApi.nwEObjectType.eObjectType_nwOaPropertyVec, null, null);
            foreach (ComApi.InwOaProperty property in (IEnumerable)attribute.Properties())
            {
                string text = property.value == null ? null : Convert.ToString(property.value, CultureInfo.InvariantCulture);
                bool target = string.Equals(Convert.ToString(property.UserName), PurgeRidConstants.PropertyName,
                    StringComparison.OrdinalIgnoreCase);
                if (target && IsPurgeValue(text))
                {
                    removed++;
                    PurgeRidDiagnosticLogger.Write("RID removed | Value=" + VisibleValue(text));
                    continue;
                }
                ComApi.InwOaProperty copy = (ComApi.InwOaProperty)state.ObjectFactory(
                    ComApi.nwEObjectType.eObjectType_nwOaProperty, null, null);
                copy.name = property.name;
                copy.UserName = property.UserName;
                copy.value = property.value;
                vector.Properties().Add(copy);
            }
            return vector;
        }

        private static string SafeItemName(ModelItem item)
        {
            try { return string.IsNullOrEmpty(item.DisplayName) ? "(unnamed)" : item.DisplayName; }
            catch { return "(unavailable)"; }
        }

        private static string VisibleValue(string value)
        {
            if (value == null) return "<null>";
            return "[" + value.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t") + "]";
        }

        private static void Release(object value)
        {
            if (value == null || !Marshal.IsComObject(value)) return;
            try { Marshal.FinalReleaseComObject(value); } catch { }
        }
    }
}
