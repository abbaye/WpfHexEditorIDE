// ==========================================================
// Project: WpfHexEditor.Editor.ResxEditor
// File: Controls/ResxResourceKeyConverter.cs
// Description:
//     Converts a resource key string to the corresponding
//     Brush by looking it up in Application.Current.Resources.
//     Used in the type-badge cell to resolve RES_*TypeBadgeBrush.
// ==========================================================

using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace WpfHexEditor.Editor.ResxEditor.Controls;

/// <summary>Looks up a resource key string in Application.Current.Resources.</summary>
[ValueConversion(typeof(string), typeof(Brush))]
public sealed class ResourceKeyToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string key && Application.Current.Resources.Contains(key))
            return Application.Current.Resources[key];
        return Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
