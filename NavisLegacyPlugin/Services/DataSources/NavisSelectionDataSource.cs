using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Autodesk.Navisworks.Api;

namespace NavisLegacyPlugin.Services.DataSources
{
	public class NavisSelectionDataSource : IDataSource
	{
		private readonly IEnumerable<ModelItem> _sourceItems;
		private readonly string _propertyTab;
		private readonly string _propertyName;

		public NavisSelectionDataSource(
			IEnumerable<ModelItem> sourceItems,
			string propertyTab,
			string propertyName)
		{
			_sourceItems = sourceItems;
			_propertyTab = propertyTab;
			_propertyName = propertyName;
		}

		public Task<DataTable> GetDataAsync(IProgress<string> progressText = null)
		{
			var table = new DataTable();

			// ✅ Standardised column names
			table.Columns.Add("GUID", typeof(string));
			table.Columns.Add(_propertyName, typeof(string));

			int index = 0;

			foreach (var item in _sourceItems)
			{
				index++;

				var guid = item.InstanceGuid.ToString();

				var value = GetPropertyValue(item, _propertyTab, _propertyName);

				if (!string.IsNullOrWhiteSpace(value))
				{
					var row = table.NewRow();
					row["GUID"] = guid;
					row[_propertyName] = value;
					table.Rows.Add(row);
				}

				// ✅ light progress (don’t spam)
				if (index % 50 == 0)
				{
					progressText?.Report($"Reading selection... {index}");
				}
			}

			progressText?.Report($"Loaded {table.Rows.Count} items from selection");

			return Task.FromResult(table);
		}

		// ✅ Safe property extraction
		private string GetPropertyValue(
			ModelItem item,
			string tabName,
			string propertyName)
		{
			foreach (var category in item.PropertyCategories)
			{
				if (!category.DisplayName.Equals(tabName))
					continue;

				foreach (var prop in category.Properties)
				{
					if (prop.DisplayName.Equals(propertyName))
					{
						return prop.Value?.ToDisplayString();
					}
				}
			}

			return null;
		}
	}
}
