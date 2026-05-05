using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace GitContextSwitcher.UI
{
    // Lightweight value converter that invokes a provided Func<T, object> to produce a binding value.
    internal class FuncValueConverter<T> : IValueConverter
    {
        private readonly Func<T?, object?> _func;

        public FuncValueConverter(Func<T?, object?> func)
        {
            _func = func ?? throw new ArgumentNullException(nameof(func));
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value is T t) return _func(t) ?? DependencyProperty.UnsetValue;
                return _func(default) ?? DependencyProperty.UnsetValue;
            }
            catch
            {
                return DependencyProperty.UnsetValue;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}
