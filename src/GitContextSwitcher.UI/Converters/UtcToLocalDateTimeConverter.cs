using System;
using System.Globalization;
using System.Windows.Data;

namespace GitContextSwitcher.UI.Converters
{
    // Converts a stored UTC DateTime to local time for display. Models persist timestamps as UTC
    // (DateTime.UtcNow), but showing raw UTC values in grids is confusing for users, so this
    // converter is applied at the binding level rather than changing storage.
    public class UtcToLocalDateTimeConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not DateTime dt) return value;

            var utc = dt.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(dt, DateTimeKind.Utc)
                : dt;

            return utc.ToLocalTime();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
