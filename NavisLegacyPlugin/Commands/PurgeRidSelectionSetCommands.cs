using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Autodesk.Navisworks.Api;
using ComApi = Autodesk.Navisworks.Api.Interop.ComApi;
using ComApiBridge = Autodesk.Navisworks.Api.ComApi;
using NavisLegacyPlugin.Models.Collections;
using NavisLegacyPlugin.Services.Collections;

namespace NavisLegacyPlugin.Commands
{
    public enum RidRemovalMode { BlankOnly, AnyRid }
    public sealed class RidCandidateResult
    {
        public int SelectedRoots { get; set; }
        public int Inspected { get; set; }
        public int Eligible { get; set; }
        public string SelectionSetName { get; set; }
    }
    public sealed class RidRemovalResult
    {
        public int Eligible { get; set; }
        public int Changed { get; set; }
        public int Removed { get; set; }
    }
    internal static class PurgeRidConstants
    {
        public const string BlankSetName = "RID-PURGE-CANDIDATES";
        public const string ClearSetName = "RID-CLEAR-CANDIDATES";
        public const string TabName = "MAE-4D";
        public const string PropertyName = "RID";
    }
    internal static class PurgeRidDiagnosticLogger
    {
        private static readonly object SyncRoot = new object();
        private static string _filePath;
        public static string Start(string mode)
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "NavisLegacy", "Diagnostics");
            Directory.CreateDirectory(folder);
            _filePath = Path.Combine(folder, "PurgeRid_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".log");
            Write("Diagnostic logging started. Mode=" + mode);
            return _filePath;
        }
        public static void Write(string message)
        {
            if (string.IsNullOrEmpty(_filePath)) return;
            lock (SyncRoot) File.AppendAllText(_filePath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) + " | " + message + Environment.NewLine, Encoding.UTF8);
        }
        public static string FilePath { get { return _filePath ?? string.Empty; } }
    }
    public static class PurgeRidWorkflow
    {
        public static RidCandidateResult PrepareCandidates(RidRemovalMode mode, IProgress<string> text, IProgress<int> percent)
        {
            Document document = Autodesk.Navisworks.Api.Application.ActiveDocument;
            if (document == null || document.Models == null || document.Models.Count == 0) throw new InvalidOperationException("There is no active model to inspect.");
            ModelItemCollection selected = document.CurrentSelection == null ? null : document.CurrentSelection.SelectedItems;
            if (selected == null || selected.Count == 0) throw new InvalidOperationException("Select the required model population before preparing RID candidates.");
            string setName = mode == RidRemovalMode.BlankOnly ? PurgeRidConstants.BlankSetName : PurgeRidConstants.ClearSetName;
            PurgeRidDiagnosticLogger.Start(mode.ToString());
            text?.Report("Resolving selected population..."); percent?.Report(10);
            CollectionResolutionResult resolved = new ModelItemCollectionResolver().Resolve(selected);
            List<ModelItem> all = resolved.AllItems.Where(x => x != null).ToList();
            var candidates = new ModelItemCollection();
            int total = all.Count;
            for (int i = 0; i < total; i++)
            {
                bool eligible = mode == RidRemovalMode.BlankOnly ? PurgeRidPropertyUtility.HasBlankRid(all[i]) : PurgeRidPropertyUtility.HasRid(all[i]);
                if (eligible) candidates.Add(all[i]);
                if (i == 0 || i + 1 == total || (i + 1) % ProgressStep(total) == 0)
                { text?.Report("Inspecting RID values " + (i + 1) + "/" + total + "..."); percent?.Report(10 + (total == 0 ? 40 : ((i + 1) * 40 / total))); DoEvents(); }
            }
            var set = new SelectionSet(candidates) { DisplayName = setName };
            int index = FindRootSetIndex(document, setName);
            if (index >= 0) document.SelectionSets.ReplaceWithCopy(index, set); else document.SelectionSets.AddCopy(set);
            PurgeRidDiagnosticLogger.Write("Candidate preparation complete. Set=" + setName + "; Inspected=" + total + "; Eligible=" + candidates.Count);
            return new RidCandidateResult { SelectedRoots = selected.Count, Inspected = total, Eligible = candidates.Count, SelectionSetName = setName };
        }
        public static RidRemovalResult RemoveCandidates(RidRemovalMode mode, IProgress<string> text, IProgress<int> percent)
        {
            Document document = Autodesk.Navisworks.Api.Application.ActiveDocument;
            if (document == null) throw new InvalidOperationException("There is no active model.");
            string setName = mode == RidRemovalMode.BlankOnly ? PurgeRidConstants.BlankSetName : PurgeRidConstants.ClearSetName;
            SelectionSet set = FindRootSelectionSet(document, setName);
            if (set == null) throw new InvalidOperationException("Prepare " + setName + " first.");
            List<ModelItem> items = set.GetSelectedItems(document).Cast<ModelItem>().Where(x => x != null).ToList();
            int eligible = items.Count(x => mode == RidRemovalMode.BlankOnly ? PurgeRidPropertyUtility.HasBlankRid(x) : PurgeRidPropertyUtility.HasRid(x));
            var result = new RidRemovalResult { Eligible = eligible };
            int processed = 0;
            foreach (ModelItem item in items)
            {
                bool stillEligible = mode == RidRemovalMode.BlankOnly ? PurgeRidPropertyUtility.HasBlankRid(item) : PurgeRidPropertyUtility.HasRid(item);
                if (stillEligible)
                {
                    int removed;
                    if (PurgeRidPropertyUtility.Remove(item, mode, out removed)) { result.Changed++; result.Removed += removed; }
                }
                processed++;
                if (processed == 1 || processed == items.Count || processed % ProgressStep(items.Count) == 0)
                { text?.Report((mode == RidRemovalMode.BlankOnly ? "Purging blank RID " : "Clearing RID ") + processed + "/" + items.Count + "..."); percent?.Report(50 + (items.Count == 0 ? 50 : processed * 50 / items.Count)); DoEvents(); }
            }
            PurgeRidDiagnosticLogger.Write("Operation complete. Mode=" + mode + "; Eligible=" + eligible + "; ItemsChanged=" + result.Changed + "; PropertiesRemoved=" + result.Removed);
            return result;
        }
        private static int ProgressStep(int total) { return total <= 100 ? 5 : 25; }
        private static void DoEvents() { System.Windows.Application.Current.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background); }
        private static int FindRootSetIndex(Document document, string name)
        { for (int i = 0; i < document.SelectionSets.RootItem.Children.Count; i++) if (document.SelectionSets.RootItem.Children[i] is SelectionSet && string.Equals(document.SelectionSets.RootItem.Children[i].DisplayName, name, StringComparison.OrdinalIgnoreCase)) return i; return -1; }
        private static SelectionSet FindRootSelectionSet(Document document, string name)
        { return document.SelectionSets.RootItem.Children.Cast<SavedItem>().OfType<SelectionSet>().FirstOrDefault(x => string.Equals(x.DisplayName, name, StringComparison.OrdinalIgnoreCase)); }
    }
    internal static class PurgeRidPropertyUtility
    {
        public static bool HasBlankRid(ModelItem item) { return Inspect(item, true); }
        public static bool HasRid(ModelItem item) { return Inspect(item, false); }
        private static bool Inspect(ModelItem item, bool blankOnly)
        {
            ComApi.InwOaPath path = null; ComApi.InwGUIPropertyNode2 node = null;
            try
            {
                ComApi.InwOpState10 state = ComApiBridge.ComApiBridge.State;
                path = ComApiBridge.ComApiBridge.ToInwOaPath(item);
                node = (ComApi.InwGUIPropertyNode2)state.GetGUIPropertyNode(path, true);
                foreach (ComApi.InwGUIAttribute2 attribute in (IEnumerable)node.GUIAttributes())
                {
                    if (attribute == null || !attribute.UserDefined || !string.Equals(Convert.ToString(attribute.ClassUserName), PurgeRidConstants.TabName, StringComparison.OrdinalIgnoreCase)) continue;
                    foreach (ComApi.InwOaProperty property in (IEnumerable)attribute.Properties())
                    {
                        if (property == null || !string.Equals(Convert.ToString(property.UserName), PurgeRidConstants.PropertyName, StringComparison.OrdinalIgnoreCase)) continue;
                        string value = property.value == null ? null : Convert.ToString(property.value, CultureInfo.InvariantCulture);
                        if (!blankOnly || IsPurgeValue(value)) return true;
                    }
                }
                return false;
            }
            finally { Release(node); Release(path); }
        }
        private static bool IsPurgeValue(string text) { return text == null || string.IsNullOrWhiteSpace(text) || text == "\"\""; }
        public static bool Remove(ModelItem item, RidRemovalMode mode, out int removedCount)
        {
            removedCount = 0; ComApi.InwOaPath path = null; ComApi.InwGUIPropertyNode2 node = null;
            try
            {
                ComApi.InwOpState10 state = ComApiBridge.ComApiBridge.State;
                path = ComApiBridge.ComApiBridge.ToInwOaPath(item); node = (ComApi.InwGUIPropertyNode2)state.GetGUIPropertyNode(path, true);
                int userDefinedIndex = 1;
                foreach (ComApi.InwGUIAttribute2 attribute in (IEnumerable)node.GUIAttributes())
                {
                    if (attribute == null || !attribute.UserDefined) continue;
                    if (string.Equals(Convert.ToString(attribute.ClassUserName), PurgeRidConstants.TabName, StringComparison.OrdinalIgnoreCase))
                    {
                        int removed; ComApi.InwOaPropertyVec replacement = BuildReplacement(state, attribute, mode, out removed);
                        try { if (removed > 0) { node.SetUserDefined(userDefinedIndex, attribute.ClassUserName, attribute.ClassName, replacement); removedCount += removed; } }
                        finally { Release(replacement); }
                    }
                    userDefinedIndex++;
                }
                return removedCount > 0;
            }
            finally { Release(node); Release(path); }
        }
        private static ComApi.InwOaPropertyVec BuildReplacement(ComApi.InwOpState10 state, ComApi.InwGUIAttribute2 attribute, RidRemovalMode mode, out int removed)
        {
            removed = 0; var vector = (ComApi.InwOaPropertyVec)state.ObjectFactory(ComApi.nwEObjectType.eObjectType_nwOaPropertyVec, null, null);
            foreach (ComApi.InwOaProperty property in (IEnumerable)attribute.Properties())
            {
                string text = property.value == null ? null : Convert.ToString(property.value, CultureInfo.InvariantCulture);
                bool target = string.Equals(Convert.ToString(property.UserName), PurgeRidConstants.PropertyName, StringComparison.OrdinalIgnoreCase);
                if (target && (mode == RidRemovalMode.AnyRid || IsPurgeValue(text))) { removed++; continue; }
                ComApi.InwOaProperty copy = (ComApi.InwOaProperty)state.ObjectFactory(ComApi.nwEObjectType.eObjectType_nwOaProperty, null, null);
                copy.name = property.name; copy.UserName = property.UserName; copy.value = property.value; vector.Properties().Add(copy);
            }
            return vector;
        }
        private static void Release(object value) { if (value != null && Marshal.IsComObject(value)) try { Marshal.FinalReleaseComObject(value); } catch { } }
    }
}
