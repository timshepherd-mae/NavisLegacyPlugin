using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.ComApi;
using Autodesk.Navisworks.Api.Interop.ComApi;
using System;
using System.Collections;
using System.Runtime.InteropServices;

namespace NavisLegacyPlugin.Services
{
	public class ComPropertyWriteService
	{
		// Internal name Navisworks commonly uses for user-defined categories
		private const string UserDefinedInternalCategoryName = "LcOaPropOverrideCat";

		/// <summary>
		/// Writes/updates the given property on every item in the current selection.
		/// Uses one shared COM state for the whole batch.
		/// </summary>
		public void WriteToCurrentSelection(
			string tabName,
			string propertyName,
			string propertyValue)
		{
			var doc = Application.ActiveDocument;

			if (doc == null)
				throw new InvalidOperationException("No active document.");

			if (doc.CurrentSelection.SelectedItems == null || doc.CurrentSelection.SelectedItems.Count == 0)
				throw new InvalidOperationException("No items selected.");

			InwOpState10 state = null;

			try
			{
				// IMPORTANT: get COM state once for the whole batch
				state = (InwOpState10)ComApiBridge.State;

				foreach (ModelItem item in doc.CurrentSelection.SelectedItems)
				{
					if (item != null)
					{
						WriteOrUpdateUserDefinedPropertyInternal(
							state,
							item,
							tabName,
							propertyName,
							propertyValue);
					}
				}
			}
			finally
			{
				// DO NOT release state.
				// Releasing ComApiBridge.State causes "separated from underlying RCW" failures
				// on subsequent items in the same batch. This follows from the issue in your
				// current per-item release pattern. 
			}
		}

		/// <summary>
		/// Optional helper retained for targeted testing by GUID within the current selection.
		/// </summary>
		public void WriteTestPropertyFromSelection(
			Guid targetGuid,
			string tabName,
			string propertyName,
			string propertyValue)
		{
			var doc = Application.ActiveDocument;
			if (doc == null)
				throw new InvalidOperationException("No active Navisworks document.");

			ModelItem target = null;

			foreach (var item in doc.CurrentSelection.SelectedItems)
			{
				if (item != null && item.InstanceGuid == targetGuid)
				{
					target = item;
					break;
				}
			}

			if (target == null)
				throw new InvalidOperationException("Target GUID not found in current selection.");

			InwOpState10 state = null;

			try
			{
				state = (InwOpState10)ComApiBridge.State;

				WriteOrUpdateUserDefinedPropertyInternal(
					state,
					target,
					tabName,
					propertyName,
					propertyValue);
			}
			finally
			{
				// DO NOT release state
			}
		}

		/// <summary>
		/// Public single-item entry point.
		/// </summary>
		public void WriteUserDefinedProperty(
			ModelItem item,
			string tabName,
			string propertyName,
			string propertyValue)
		{
			if (item == null)
				throw new ArgumentNullException(nameof(item));

			InwOpState10 state = null;

			try
			{
				state = (InwOpState10)ComApiBridge.State;

				WriteOrUpdateUserDefinedPropertyInternal(
					state,
					item,
					tabName,
					propertyName,
					propertyValue);
			}
			finally
			{
				// DO NOT release state
			}
		}

		/// <summary>
		/// Core logic:
		/// - if target tab already exists, rebuild its property vector and overwrite it
		/// - if target tab does not exist, create it as a new user-defined tab
		/// </summary>
		private void WriteOrUpdateUserDefinedPropertyInternal(
			InwOpState10 state,
			ModelItem item,
			string tabName,
			string propertyName,
			string propertyValue)
		{
			if (state == null)
				throw new ArgumentNullException(nameof(state));

			if (item == null)
				throw new ArgumentNullException(nameof(item));

			if (string.IsNullOrWhiteSpace(tabName))
				throw new ArgumentException("tabName is required.");

			if (string.IsNullOrWhiteSpace(propertyName))
				throw new ArgumentException("propertyName is required.");

			InwOaPath path = null;
			InwGUIPropertyNode node = null;
			InwGUIPropertyNode2 node2 = null;
			InwOaPropertyVec propVec = null;

			try
			{
				path = (InwOaPath)ComApiBridge.ToInwOaPath(item);

				node = state.GetGUIPropertyNode(path, true);
				node2 = (InwGUIPropertyNode2)node;

				int existingUserDefinedIndex;
				InwGUIAttribute2 existingAttr;

				existingUserDefinedIndex = FindExistingUserDefinedTab(
					node2,
					tabName,
					out existingAttr);

				if (existingUserDefinedIndex > 0 && existingAttr != null)
				{
					// Existing tab found:
					// rebuild full vector, replace/append the target property,
					// then overwrite the existing user-defined category.
					propVec = BuildUpdatedPropertyVector(
						state,
						existingAttr,
						propertyName,
						propertyValue);

					node2.SetUserDefined(
						existingUserDefinedIndex,
						existingAttr.ClassUserName,
						existingAttr.ClassName,
						propVec);
				}
				else
				{
					// No matching tab found:
					// Autodesk examples show index 0 for creating a NEW user-defined category.
					propVec = CreateSinglePropertyVector(
						state,
						propertyName,
						propertyValue);

					node2.SetUserDefined(
						0,
						tabName,
						UserDefinedInternalCategoryName,
						propVec);
				}
			}
			finally
			{
				// release ONLY per-item COM objects
				SafeRelease(propVec);
				SafeRelease(node2);
				SafeRelease(node);
				SafeRelease(path);
			}
		}

		/// <summary>
		/// Finds an existing user-defined tab by visible/custom tab name.
		/// IMPORTANT:
		/// Autodesk examples and accepted solution show that the first existing
		/// user-defined category is index 1, not 0.
		/// </summary>
		private int FindExistingUserDefinedTab(
			InwGUIPropertyNode2 node2,
			string targetTabName,
			out InwGUIAttribute2 foundAttribute)
		{
			foundAttribute = null;

			if (node2 == null)
				return -1;

			// 1-based index for existing user-defined tabs
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

		/// <summary>
		/// Creates a new property vector containing one property.
		/// </summary>
		private InwOaPropertyVec CreateSinglePropertyVector(
			InwOpState10 state,
			string propertyName,
			string propertyValue)
		{
			var propVec = (InwOaPropertyVec)state.ObjectFactory(
				nwEObjectType.eObjectType_nwOaPropertyVec,
				null,
				null);

			InwOaProperty prop = null;

			try
			{
				prop = CreateStringProperty(state, propertyName, propertyValue);
				propVec.Properties().Add(prop);

				// COM object now owned by vector
				prop = null;

				return propVec;
			}
			finally
			{
				SafeRelease(prop);
			}
		}

		/// <summary>
		/// Rebuilds the full property vector for an existing user-defined tab:
		/// - copies all existing properties
		/// - replaces (upserts) the target property
		/// </summary>
		private InwOaPropertyVec BuildUpdatedPropertyVector(
			InwOpState10 state,
			InwGUIAttribute2 existingAttr,
			string newPropertyName,
			string newPropertyValue)
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

					string existingInternalName = SafeString(p.name);
					string existingUserName = SafeString(p.UserName);

					bool sameProperty =
						string.Equals(existingInternalName, newPropertyName, StringComparison.OrdinalIgnoreCase) ||
						string.Equals(existingUserName, newPropertyName, StringComparison.OrdinalIgnoreCase);

					if (sameProperty)
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

						// COM object now owned by vector
						copy = null;
					}
					finally
					{
						SafeRelease(copy);
					}
				}
			}

			// Add/replace the requested property
			InwOaProperty newProp = null;

			try
			{
				newProp = CreateStringProperty(state, newPropertyName, newPropertyValue);
				propVec.Properties().Add(newProp);
				newProp = null;
			}
			finally
			{
				SafeRelease(newProp);
			}

			return propVec;
		}

		private InwOaProperty CreateStringProperty(
			InwOpState10 state,
			string propertyName,
			string propertyValue)
		{
			var prop = (InwOaProperty)state.ObjectFactory(
				nwEObjectType.eObjectType_nwOaProperty,
				null,
				null);

			prop.name = propertyName;
			prop.UserName = propertyName;
			prop.value = propertyValue;

			return prop;
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
				// Never throw during COM cleanup
			}
		}
	}
}
