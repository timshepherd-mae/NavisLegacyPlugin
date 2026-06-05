using System;
using System.Collections.Generic;
using Autodesk.Navisworks.Api;

namespace NavisLegacyPlugin.Services
{
	public class ModelLookupService
	{
		public Dictionary<string, ModelItem> BuildLookup(string tabDisplayName, string propertyDisplayName)
		{
			var lookup = new Dictionary<string, ModelItem>(StringComparer.OrdinalIgnoreCase);

			var doc = Application.ActiveDocument;
			if (doc == null)
				return lookup;

			foreach (Model model in doc.Models)
			{
				foreach (ModelItem item in model.RootItem.DescendantsAndSelf)
				{
					foreach (PropertyCategory cat in item.PropertyCategories)
					{
						if (!string.Equals(cat.DisplayName, tabDisplayName, StringComparison.OrdinalIgnoreCase))
							continue;

						foreach (DataProperty prop in cat.Properties)
						{
							if (!string.Equals(prop.DisplayName, propertyDisplayName, StringComparison.OrdinalIgnoreCase))
								continue;

							var value = prop.Value != null ? prop.Value.ToDisplayString() : null;

							if (!string.IsNullOrWhiteSpace(value) && !lookup.ContainsKey(value))
								lookup[value] = item;
						}
					}
				}
			}

			return lookup;
		}
	}
}