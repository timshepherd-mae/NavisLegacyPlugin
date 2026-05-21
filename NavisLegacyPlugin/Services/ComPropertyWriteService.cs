using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.ComApi;
using Autodesk.Navisworks.Api.Interop.ComApi;
using System;
using System.Runtime.InteropServices;

namespace NavisLegacyPlugin.Services
{
	public class ComPropertyWriteService
	{
		// ✅ Helper method used by the ViewModel
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

			WriteUserDefinedProperty(target, tabName, propertyName, propertyValue);
		}

		// ✅ Core COM write logic
		public void WriteUserDefinedProperty(
			ModelItem item,
			string tabName,
			string propertyName,
			string propertyValue)
		{
			InwOpState10 state = null;
			InwOaPath path = null;
			InwGUIPropertyNode node = null;
			InwGUIPropertyNode2 node2 = null;
			InwOaPropertyVec propVec = null;

			try
			{
				// 1) COM state
				state = (InwOpState10)ComApiBridge.State;

				// 2) ModelItem → COM path
				path = (InwOaPath)ComApiBridge.ToInwOaPath(item);

				// 3) GUI property node
				node = state.GetGUIPropertyNode(path, true);

				// 4) Cast to v2 interface (this is the key discovery you made)
				node2 = (InwGUIPropertyNode2)node;

				// 5) Create property vector
				propVec = (InwOaPropertyVec)state.ObjectFactory(
					nwEObjectType.eObjectType_nwOaPropertyVec,
					null,
					null);

				// 6) Create property
				InwOaProperty prop = (InwOaProperty)state.ObjectFactory(
					nwEObjectType.eObjectType_nwOaProperty,
					null,
					null);

				prop.name = propertyName;
				prop.value = propertyValue;

				propVec.Properties().Add(prop);

				// ✅ 7) WRITE USER DEFINED PROPERTY
				node2.SetUserDefined(
					0,              // index (0 = overwrite / first)
					tabName,        // user-visible tab
					tabName,        // internal name
					propVec);
			}
			finally
			{
				SafeRelease(propVec);
				SafeRelease(node2);
				SafeRelease(node);
				SafeRelease(path);
				SafeRelease(state);
			}
		}

		// ✅ COM cleanup helper
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
