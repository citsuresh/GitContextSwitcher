using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace GitContextSwitcher.UI.Converters
{
    // Converter: maps file change type (enum or string) to a Brush for the preview tree rectangle.
    public class FileChangeTypeToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value == null) return System.Windows.Media.Brushes.Transparent;
                var s = value.ToString() ?? string.Empty;
                switch (s.ToLowerInvariant())
                {
                    case "added":
                    case "1":
                        return System.Windows.Media.Brushes.LimeGreen;
                    case "modified":
                    case "2":
                        return System.Windows.Media.Brushes.Orange;
                    case "deleted":
                    case "3":
                        return System.Windows.Media.Brushes.Tomato;
                    default:
                        return System.Windows.Media.Brushes.Transparent;
                }
            }
            catch
            {
                return System.Windows.Media.Brushes.Transparent;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
