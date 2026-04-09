using System;
using System.Globalization;
using System.Windows.Data;

namespace WpfHexEditor.Core.Settings.Converters
{
    /// <summary>
    /// Converter for double zoom value to percentage string (e.g., 1.0 -> "100%")
    /// </summary>
    public class ZoomToPercentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double zoom)
            {
                return $"{zoom * 100:0}%";
            }
            return "100%";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
