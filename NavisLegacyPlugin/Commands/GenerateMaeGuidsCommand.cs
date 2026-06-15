using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Plugins;
using ComApi = Autodesk.Navisworks.Api.Interop.ComApi;
using ComApiBridge = Autodesk.Navisworks.Api.ComApi;

namespace NavisLegacyPlugin.Commands
{
	[Plugin("GenerateMaeGuids", "MAE",
		DisplayName = "Generate MAE GUIDs",
		ToolTip = "Assign GUIDs to selected branches, their descendants, and their ancestor chain")]
	public class GenerateMaeGuidsCommand : AddInPlugin
	{
		public override int Execute(params string[] parameters)
		{
			Document doc = Application.ActiveDocument;
			ModelItemCollection selectedItems = doc.CurrentSelection.SelectedItems;




			if (selectedItems == null || selectedItems.Count == 0)
				return 0;

			var targets = new HashSet<ModelItem>();

			foreach (ModelItem selected in selectedItems)
			{
				foreach (ModelItem item in GetTargetItems(selected))
				{
					targets.Add(item);
				}
			}

			foreach (ModelItem item in targets)
			{
				WriteUserDefinedGuid(item, Guid.NewGuid().ToString());
			}

			return 0;
		}

		/// <summary>
		/// Returns:
		/// 1) the selected item
		/// 2) all descendants below it
		/// 3) all ancestors above it to model-top
		/// Does NOT include descendants of any ancestor except the selected branch itself.
		/// </summary>
		private IEnumerable<ModelItem> GetTargetItems(ModelItem item)
		{
			if (item == null)
				yield break;

			// Include selected item + everything below it
			foreach (ModelItem sub in GetDescendantsIncludingSelf(item))
				yield return sub;

			// Include parent chain above it, but NOT siblings/other branches
			ModelItem current = item.Parent;
			while (current != null)
			{
				yield return current;
				current = current.Parent;
			}
		}

		/// <summary>
		/// Depth-first traversal of the selected branch only.
		/// Includes the starting item.
		/// </summary>
		private IEnumerable<ModelItem> GetDescendantsIncludingSelf(ModelItem item)
		{
			if (item == null)
				yield break;

			yield return item;

			if (item.Children == null || item.Children.Count() == 0)
				yield break;

			foreach (ModelItem child in item.Children)
			{
				foreach (ModelItem sub in GetDescendantsIncludingSelf(child))
					yield return sub;
			}
		}

		private void WriteUserDefinedGuid(ModelItem item, string guidValue)
		{
			ComApi.InwOpState10 state = ComApiBridge.ComApiBridge.State;

			ComApi.InwOaPath path = ComApiBridge.ComApiBridge.ToInwOaPath(item);

			ComApi.InwGUIPropertyNode2 propNode =
				(ComApi.InwGUIPropertyNode2)state.GetGUIPropertyNode(path, true);

			ComApi.InwOaPropertyVec propVec =
				(ComApi.InwOaPropertyVec)state.ObjectFactory(
					ComApi.nwEObjectType.eObjectType_nwOaPropertyVec, null, null);

			ComApi.InwOaProperty prop =
				(ComApi.InwOaProperty)state.ObjectFactory(
					ComApi.nwEObjectType.eObjectType_nwOaProperty, null, null);

			prop.name = "GUID_InternalName";
			prop.UserName = "GUID";
			prop.value = guidValue;

			propVec.Properties().Add(prop);

			propNode.SetUserDefined(0, "MAE-4D", "MAE-4D_InternalName", propVec);
		}
	}
}