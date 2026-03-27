// ==========================================================
// Project: WpfHexEditor.Plugins.Debugger
// File: Converters/DebugConverters.cs
// Description:
//     Value converters used by debug panel XAML (BreakpointsPanel, CallStackPanel).
// Architecture:
//     Plugin-local — no dependency beyond WPF.
// ==========================================================

using System.Globalization;
using System.Windows.Data;

namespace WpfHexEditor.Plugins.Debugger.Converters;

/// <summary>Converts a boolean IsEnabled to opacity (1.0 = enabled, 0.45 = disabled).</summary>
internal sealed class BoolToOpacityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? 1.0 : 0.45;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Converts a boolean IsVerified to a checkmark glyph (or empty).</summary>
internal sealed class BoolToVerifiedConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? "\u2713" : "";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Extracts the file name from a full path string.</summary>
internal sealed class FileNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is string path ? System.IO.Path.GetFileName(path) : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
