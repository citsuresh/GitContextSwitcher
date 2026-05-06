using System;
using System.Globalization;
using System.Windows.Data;

namespace GitContextSwitcher.UI.Converters
{
    // Converts any non-null object to true, null to false. Useful for enabling buttons when selection exists.
    public class ObjectToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
