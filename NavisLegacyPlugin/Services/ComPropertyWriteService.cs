using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.ComApi;
using Autodesk.Navisworks.Api.Interop.ComApi;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace NavisLegacyPlugin.Services
{
	public class ComPropertyWriteService
	{
		private const string UserDefinedInternalCategoryName = "LcOaPropOverrideCat";

		/// <summary>
		/// Writes/updates a set of properties to every item in the current selection.
		/// </summary>
		public void WriteToCurrentSelection(
			string tabName,
			IDictionary<string, string> properties)
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

			InwOpState10 state = null;

			try
			{
				// IMPORTANT: get COM state ONCE for the whole batch
				state = (InwOpState10)ComApiBridge.State;

				foreach (ModelItem item in doc.CurrentSelection.SelectedItems)
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
				// Releasing ComApiBridge.State is what caused the RCW separation issue
				// in the earlier per-item pattern.
			}
		}

		/// <summary>
		/// Convenience single-item entry point.
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

		/// <summary>
		/// Backward-compatible helper if you still want a one-property call site.
		/// </summary>
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

		/// <summary>
		/// Backward-compatible selection helper for one property.
		/// </summary>
		public void WriteToCurrentSelection(
			string tabName,
			string propertyName,
			string propertyValue)
		{
			var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
			{
				{ propertyName, propertyValue }
			};

			WriteToCurrentSelection(tabName, properties);
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
					// Existing tab found:
					// build a new vector that preserves unrelated existing properties
					// and upserts all supplied properties.
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
					// New tab:
					// Autodesk examples show index 0 must be used to create a new user-defined category.
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

		/// <summary>
		/// Finds an existing user-defined tab by visible/custom tab name.
		/// IMPORTANT:
		/// first existing user-defined tab is index 1, not 0.
		/// </summary>
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
		/// Create a new property vector from the supplied dictionary.
		/// </summary>
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

		/// <summary>
		/// Merge supplied properties into an existing user-defined category:
		/// - preserve existing properties that are not being replaced
		/// - overwrite matching properties
		/// - append any new ones
		/// </summary>
		private InwOaPropertyVec BuildMergedPropertyVector(
			InwOpState10 state,
			InwGUIAttribute2 existingAttr,
			IDictionary<string, string> incomingProperties)
		{
			var propVec = (InwOaPropertyVec)state.ObjectFactory(
				nwEObjectType.eObjectType_nwOaPropertyVec,
				null,
				null);

			// 1) Copy all existing properties EXCEPT the ones we are replacing
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

						// ownership transferred to propVec
						copy = null;
					}
					finally
					{
						SafeRelease(copy);
					}
				}
			}

			// 2) Add all incoming properties (overwrite/append)
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

				// ownership transferred to propVec
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