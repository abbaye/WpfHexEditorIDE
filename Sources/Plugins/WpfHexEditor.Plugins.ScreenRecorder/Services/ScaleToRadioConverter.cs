// Project: WpfHexEditor.Plugins.ScreenRecorder
// File: Services/ScaleToRadioConverter.cs
// Description: IValueConverter that maps OutputScale (double) to a bool for RadioButton binding.

using System.Globalization;
using System.Windows.Data;

namespace WpfHexEditor.Plugins.ScreenRecorder.Services;

public sealed class ScaleToRadioConverter : IValueConverter
{
    public static readonly ScaleToRadioConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double scale && parameter is string s && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var target))
            return Math.Abs(scale - target) < 0.001;
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is true && parameter is string s && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var target))
            return target;
        return Binding.DoNothing;
    }
}
