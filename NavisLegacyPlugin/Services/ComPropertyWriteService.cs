using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.ComApi;
using Autodesk.Navisworks.Api.Interop.ComApi;

namespace NavisLegacyPlugin.Services
{
	public class ComPropertyWriteService
	{
		private const string UserDefinedInternalCategoryName = "LcOaPropOverrideCat";

		public void WriteToCurrentSelection(
			string tabName,
			IDictionary<string, string> properties,
			bool writeToLeafItems)
		{
			var doc = Application.ActiveDocument;

			if (doc == null)
				throw new InvalidOperationException("No active document.");

			if (doc.CurrentSelection.SelectedItems == null || doc.CurrentSelection.SelectedItems.Count() == 0)
				throw new InvalidOperationException("No items selected.");

			if (string.IsNullOrWhiteSpace(tabName))
				throw new ArgumentException("tabName is required.");

			if (properties == null || properties.Count == 0)
				throw new ArgumentException("At least one property is required.");

			var targetItems = new List<ModelItem>();

			// ✅ FIXED: Proper branch vs leaf logic
			if (writeToLeafItems)
			{
				foreach (ModelItem item in doc.CurrentSelection.SelectedItems)
				{
					CollectLeafItems(item, targetItems);
				}
			}
			else
			{
				foreach (ModelItem item in doc.CurrentSelection.SelectedItems)
				{
					if (item != null)
						targetItems.Add(item);
				}
			}

			// ✅ DEBUG (you can remove later)
			System.Diagnostics.Debug.WriteLine($"writeToLeafItems = {writeToLeafItems}");
			System.Diagnostics.Debug.WriteLine($"targetItems count = {targetItems.Count}");

			InwOpState10 state = null;

			try
			{
				state = (InwOpState10)ComApiBridge.State;

				// ✅ FIXED: Use resolved targetItems, NOT SelectedItems
				foreach (ModelItem item in targetItems)
				{
					if (item != null)
					{
						WriteOrUpdateUserDefinedPropertiesInternal(
							state,
							item,
							tabName,
							properties);
					}
				}
			}
			finally
			{
				// DO NOT release state
			}
		}

		public void WriteUserDefinedProperties(
			ModelItem item,
			string tabName,
			IDictionary<string, string> properties)
		{
			if (item == null)
				return;

			var state = (InwOpState10)ComApiBridge.State;

			WriteOrUpdateUserDefinedPropertiesInternal(
				state,
				item,
				tabName,
				properties);
		}

		public void WriteUserDefinedPropertiesToItems(
			IEnumerable<ModelItem> items,
			string tabName,
			IDictionary<string, string> properties)
		{
			if (items == null)
				throw new ArgumentNullException(nameof(items));

			if (string.IsNullOrWhiteSpace(tabName))
				throw new ArgumentException("tabName is required.");

			if (properties == null || properties.Count == 0)
				throw new ArgumentException("At least one property is required.");

			InwOpState10 state = null;

			try
			{
				state = (InwOpState10)ComApiBridge.State;

				// Optional de-duplication by InstanceGuid to avoid writing the same item twice
				var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

				foreach (var item in items)
				{
					if (item == null)
						continue;

					var guid = item.InstanceGuid.ToString("D");
					if (seen.Contains(guid))
						continue;

					seen.Add(guid);

					WriteOrUpdateUserDefinedPropertiesInternal(
						state,
						item,
						tabName,
						properties);
				}
			}
			finally
			{
				// DO NOT release state
			}
		}


		// ✅ FIXED: Leaf traversal
		private void CollectLeafItems(ModelItem item, List<ModelItem> results)
		{
			if (item == null)
				return;

			if (item.Children == null || item.Children.Count() == 0)
			{
				if (!results.Contains(item))
					results.Add(item);

				return;
			}

			foreach (ModelItem child in item.Children)
			{
				CollectLeafItems(child, results);
			}
		}

		private void WriteOrUpdateUserDefinedPropertiesInternal(
			InwOpState10 state,
			ModelItem item,
			string tabName,
			IDictionary<string, string> properties)
		{
			InwOaPath path = null;
			InwGUIPropertyNode node = null;
			InwGUIPropertyNode2 node2 = null;
			InwOaPropertyVec propVec = null;

			try
			{
				path = (InwOaPath)ComApiBridge.ToInwOaPath(item);

				node = state.GetGUIPropertyNode(path, true);
				node2 = (InwGUIPropertyNode2)node;

				InwGUIAttribute2 existingAttr;
				int existingUserDefinedIndex = FindExistingUserDefinedTab(
					node2,
					tabName,
					out existingAttr);

				if (existingUserDefinedIndex > 0 && existingAttr != null)
				{
					propVec = BuildMergedPropertyVector(
						state,
						existingAttr,
						properties);

					node2.SetUserDefined(
						existingUserDefinedIndex,
						existingAttr.ClassUserName,
						existingAttr.ClassName,
						propVec);
				}
				else
				{
					propVec = CreatePropertyVector(
						state,
						properties);

					node2.SetUserDefined(
						0,
						tabName,
						UserDefinedInternalCategoryName,
						propVec);
				}
			}
			finally
			{
				SafeRelease(propVec);
				SafeRelease(node2);
				SafeRelease(node);
				SafeRelease(path);
			}
		}

		private int FindExistingUserDefinedTab(
			InwGUIPropertyNode2 node2,
			string targetTabName,
			out InwGUIAttribute2 foundAttribute)
		{
			foundAttribute = null;

			if (node2 == null)
				return -1;

			int userDefinedIndex = 1;

			foreach (InwGUIAttribute2 attr in (IEnumerable)node2.GUIAttributes())
			{
				if (attr == null || !attr.UserDefined)
					continue;

				string classUserName = SafeString(attr.ClassUserName);

				if (string.Equals(classUserName, targetTabName, StringComparison.OrdinalIgnoreCase))
				{
					foundAttribute = attr;
					return userDefinedIndex;
				}

				userDefinedIndex++;
			}

			return -1;
		}

		private InwOaPropertyVec CreatePropertyVector(
			InwOpState10 state,
			IDictionary<string, string> properties)
		{
			var propVec = (InwOaPropertyVec)state.ObjectFactory(
				nwEObjectType.eObjectType_nwOaPropertyVec,
				null,
				null);

			foreach (var kvp in properties)
			{
				AddProperty(state, propVec, kvp.Key, kvp.Value);
			}

			return propVec;
		}

		private InwOaPropertyVec BuildMergedPropertyVector(
			InwOpState10 state,
			InwGUIAttribute2 existingAttr,
			IDictionary<string, string> incomingProperties)
		{
			var propVec = (InwOaPropertyVec)state.ObjectFactory(
				nwEObjectType.eObjectType_nwOaPropertyVec,
				null,
				null);

			if (existingAttr != null)
			{
				foreach (InwOaProperty p in (IEnumerable)existingAttr.Properties())
				{
					if (p == null)
						continue;

					string existingName = SafeString(p.name);
					string existingUserName = SafeString(p.UserName);

					bool replace =
						incomingProperties.ContainsKey(existingName) ||
						(!string.IsNullOrWhiteSpace(existingUserName) &&
						 incomingProperties.ContainsKey(existingUserName));

					if (replace)
						continue;

					InwOaProperty copy = null;

					try
					{
						copy = (InwOaProperty)state.ObjectFactory(
							nwEObjectType.eObjectType_nwOaProperty,
							null,
							null);

						copy.name = existingName;
						copy.UserName = existingUserName;
						copy.value = p.value;

						propVec.Properties().Add(copy);

						copy = null;
					}
					finally
					{
						SafeRelease(copy);
					}
				}
			}

			foreach (var kvp in incomingProperties)
			{
				AddProperty(state, propVec, kvp.Key, kvp.Value);
			}

			return propVec;
		}

		private void AddProperty(
			InwOpState10 state,
			InwOaPropertyVec propVec,
			string name,
			string value)
		{
			InwOaProperty prop = null;

			try
			{
				prop = (InwOaProperty)state.ObjectFactory(
					nwEObjectType.eObjectType_nwOaProperty,
					null,
					null);

				prop.name = name;
				prop.UserName = name;
				prop.value = value;

				propVec.Properties().Add(prop);

				prop = null;
			}
			finally
			{
				SafeRelease(prop);
			}
		}

		private static string SafeString(object value)
		{
			return value == null ? string.Empty : value.ToString();
		}

		private static void SafeRelease(object comObj)
		{
			try
			{
				if (comObj != null && Marshal.IsComObject(comObj))
					Marshal.FinalReleaseComObject(comObj);
			}
			catch { }
		}
	}
}