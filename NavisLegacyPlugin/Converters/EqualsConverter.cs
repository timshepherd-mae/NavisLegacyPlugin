using System;
using System.Globalization;
using System.Windows.Data;

namespace NavisLegacyPlugin.Converters
{
	public class EqualsConverter : IValueConverter
	{
		/// <summary>
		/// Converts the bound value to a boolean indicating whether it equals the parameter.
		/// </summary>
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value == null || parameter == null)
				return false;

			return string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>
		/// Converts back from RadioButton IsChecked to the bound value.
		/// Only returns the parameter when checked = true.
		/// </summary>
		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is bool isChecked && isChecked)
			{
				return parameter?.ToString();
			}

			return Binding.DoNothing;
		}
	}
}