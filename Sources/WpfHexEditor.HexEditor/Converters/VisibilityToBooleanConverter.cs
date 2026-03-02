//////////////////////////////////////////////
// Apache 2.0  - 2019
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WpfHexEditor.HexEditor.Converters
{
    /// <summary>
    /// This VisibilityToBoolean converter convert Visibility <-> Boolean
    /// </summary>
    public sealed class VisibilityToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            (Visibility)value == Visibility.Visible;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            (bool)value == true
                ? Visibility.Visible
                : Visibility.Collapsed;
    }
}
