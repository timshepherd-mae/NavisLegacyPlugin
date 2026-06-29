using System;
using System.Globalization;
using System.Windows.Data;

namespace NavisLegacyPlugin.Converters
{
	public class EnumToBooleanConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value == null || parameter == null)
				return false;

			return value.ToString().Equals(parameter.ToString(), StringComparison.OrdinalIgnoreCase);
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if ((bool)value)
				return Enum.Parse(targetType, parameter.ToString());

			return Binding.DoNothing;
		}
	}
}