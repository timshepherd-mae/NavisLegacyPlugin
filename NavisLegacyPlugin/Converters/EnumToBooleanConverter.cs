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


        public object ConvertBack(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture)
        {
            if ((bool)value)
            {
                Type enumType =
                    Nullable.GetUnderlyingType(targetType)
                    ?? targetType;

                return Enum.Parse(
                    enumType,
                    parameter.ToString());
            }

            return Binding.DoNothing;
        }


    }
}