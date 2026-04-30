using System;
using System.Globalization;
using System.Windows.Data;

namespace NavisLegacyPlugin.Converters
{
	public class DoubleFormatConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value == null)
				return string.Empty;

			if (value is double d)
			{
				var format = parameter as string ?? "F4";
				return d.ToString(format, culture);
			}

			return value.ToString();
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
}
