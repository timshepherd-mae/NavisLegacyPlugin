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

		/// <summary>
		/// Writes/updates a set of properties to the current selection.
		/// writeToLeafItems = false  -> selected items only
		/// writeToLeafItems = true   -> all leaf items beneath each selected item
		/// </summary>
		public void WriteToCurrentSelection(
			string tabName,
			IDictionary<string, string> properties,
			bool writeToLeafItems)
		{
			var doc = Application.ActiveDocument;

			if (doc == null)
				throw new InvalidOperationException("No active document.");

			if (doc.CurrentSelection.SelectedItems == null || doc.CurrentSelection.SelectedItems.Count == 0)
				throw new InvalidOperationException("No items selected.");

			if (string.IsNullOrWhiteSpace(tabName))
				throw new ArgumentException("tabName is required.");

			if (properties == null || properties.Count == 0)
				throw new ArgumentException("At least one property is required.");

			var targetItems = GetTargetItemsFromSelection(doc.CurrentSelection.SelectedItems, writeToLeafItems);

			if (targetItems.Count == 0)
				throw new InvalidOperationException("No target items found to write to.");

			InwOpState10 state = null;

			try
			{
				// IMPORTANT: one shared COM state for the whole batch
				state = (InwOpState10)ComApiBridge.State;

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
				// DO NOT release state.
				// Releasing ComApiBridge.State can cause RCW separation issues.
			}
		}

		/// <summary>
		/// Convenience overload for old single-property call sites.
		/// </summary>
		public void WriteToCurrentSelection(
			string tabName,
			string propertyName,
			string propertyValue,
			bool writeToLeafItems)
		{
			var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
			{
				{ propertyName, propertyValue }
			};

			WriteToCurrentSelection(tabName, properties, writeToLeafItems);
		}

		/// <summary>
		/// Single item entry point.
		/// </summary>
		public void WriteUserDefinedProperties(
			ModelItem item,
			string tabName,
			IDictionary<string, string> properties)
		{
			if (item == null)
				throw new ArgumentNullException(nameof(item));

			if (string.IsNullOrWhiteSpace(tabName))
				throw new ArgumentException("tabName is required.");

			if (properties == null || properties.Count == 0)
				throw new ArgumentException("At least one property is required.");

			InwOpState10 state = null;

			try
			{
				state = (InwOpState10)ComApiBridge.State;

				WriteOrUpdateUserDefinedPropertiesInternal(
					state,
					item,
					tabName,
					properties);
			}
			finally
			{
				// DO NOT release state
			}
		}

		public void WriteUserDefinedProperty(
			ModelItem item,
			string tabName,
			string propertyName,
			string propertyValue)
		{
			var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
			{
				{ propertyName, propertyValue }
			};

			WriteUserDefinedProperties(item, tabName, properties);
		}

		private List<ModelItem> GetTargetItemsFromSelection(
			ModelItemCollection selectedItems,
			bool writeToLeafItems)
		{
			var results = new List<ModelItem>();

			if (selectedItems == null || selectedItems.Count == 0)
				return results;

			if (!writeToLeafItems)
			{
				foreach (ModelItem item in selectedItems)
				{
					if (item != null && !results.Contains(item))
						results.Add(item);
				}

				return results;
			}

			foreach (ModelItem item in selectedItems)
			{
				if (item == null)
					continue;

				CollectLeafItems(item, results);
			}

			return results;
		}

		private void CollectLeafItems(ModelItem item, List<ModelItem> results)
		{
			if (item == null)
				return;

			// Leaf = no children
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
					// Existing tab found: rebuild full vector and overwrite
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
					// No matching tab found: create a new one
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

			// IMPORTANT: first existing user-defined tab index is 1, not 0
			int userDefinedIndex = 1;

			foreach (InwGUIAttribute2 attr in (IEnumerable)node2.GUIAttributes())
			{
				if (attr == null)
					continue;

				if (!attr.UserDefined)
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

			// Preserve existing properties that are not being replaced
			if (existingAttr != null)
			{
				foreach (InwOaProperty p in (IEnumerable)existingAttr.Properties())
				{
					if (p == null)
						continue;

					string existingInternalName = SafeString(p.name);
					string existingUserName = SafeString(p.UserName);

					bool isBeingReplaced =
						incomingProperties.ContainsKey(existingInternalName) ||
						(!string.IsNullOrWhiteSpace(existingUserName) && incomingProperties.ContainsKey(existingUserName));

					if (isBeingReplaced)
						continue;

					InwOaProperty copy = null;

					try
					{
						copy = (InwOaProperty)state.ObjectFactory(
							nwEObjectType.eObjectType_nwOaProperty,
							null,
							null);

						copy.name = existingInternalName;
						copy.UserName = string.IsNullOrWhiteSpace(existingUserName)
							? existingInternalName
							: existingUserName;
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

			// Add/update all incoming properties
			foreach (var kvp in incomingProperties)
			{
				AddProperty(state, propVec, kvp.Key, kvp.Value);
			}

			return propVec;
		}

		private void AddProperty(
			InwOpState10 state,
			InwOaPropertyVec propVec,
			string propertyName,
			string propertyValue)
		{
			InwOaProperty prop = null;

			try
			{
				prop = (InwOaProperty)state.ObjectFactory(
					nwEObjectType.eObjectType_nwOaProperty,
					null,
					null);

				prop.name = propertyName;
				prop.UserName = propertyName;
				prop.value = propertyValue;

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
			catch
			{
				// never throw during COM cleanup
			}
		}
	}
}