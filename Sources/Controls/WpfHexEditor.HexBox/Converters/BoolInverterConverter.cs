//////////////////////////////////////////////
// Apache 2.0  - 2016-2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Globalization;
using System.Windows.Data;

namespace WpfHexEditor.HexBox.Converters
{
    /// <summary>
    /// Invert bool
    /// </summary>
    public sealed class BoolInverterConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool? val = null;

            try
            {
                val = value is not null && (bool)value;
            }
            catch
            {
                // ignored
            }

            return !val;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            Binding.DoNothing;
    }
}
