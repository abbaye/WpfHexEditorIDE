//////////////////////////////////////////////
// Project      : WpfHexEditor.Editor.StructureEditor
// File         : Controls/HexStringToColorConverter.cs
// Description  : IValueConverter that bridges a hex color string (e.g. #FF8C00 or #AARRGGBB)
//                stored in the ViewModel with the ColorPicker.SelectedColor (System.Windows.Media.Color).
// Architecture : Stateless converter; ConvertBack produces uppercase #AARRGGBB string.
//////////////////////////////////////////////

using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace WpfHexEditor.Editor.StructureEditor.Controls;

[ValueConversion(typeof(string), typeof(Color))]
public sealed class HexStringToColorConverter : IValueConverter
{
    public static readonly HexStringToColorConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string hex)
        {
            try { return (Color)ColorConverter.ConvertFromString(hex); }
            catch { /* fall through */ }
        }
        return Colors.White;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Color c)
            return c.A == 255
                ? $"#{c.R:X2}{c.G:X2}{c.B:X2}"
                : $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";

        return "#FFFFFF";
    }
}
