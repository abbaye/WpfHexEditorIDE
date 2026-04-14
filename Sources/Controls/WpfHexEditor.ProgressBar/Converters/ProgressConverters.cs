// ==========================================================
// Project: WpfHexEditor.ProgressBar
// File: Converters/ProgressConverters.cs
// Description:
//     Value converters for common progress bar bindings.
// ==========================================================

using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WpfHexEditor.ProgressBar.Converters;

/// <summary>Converts a double (0–1) to a percentage string like "45%".</summary>
public sealed class ProgressToPercentStringConverter : IValueConverter
{
    public static readonly ProgressToPercentStringConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double d)
            return $"{(int)Math.Round(d * 100)}%";
        return "0%";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>Converts a double progress to Visibility: &gt; 0 → Visible, else Collapsed.</summary>
public sealed class ProgressToVisibilityConverter : IValueConverter
{
    public static readonly ProgressToVisibilityConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double d && d > 0)
            return Visibility.Visible;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>
/// Converts a value in range [0, Maximum] to [0, 1] for use with <see cref="Controls.LinearProgressBar"/>.
/// Pass the maximum as ConverterParameter (default 100).
/// </summary>
public sealed class ValueToProgressConverter : IValueConverter
{
    public static readonly ValueToProgressConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        double max = 100;
        if (parameter is string s && double.TryParse(s, CultureInfo.InvariantCulture, out var parsed))
            max = parsed;
        else if (parameter is double d)
            max = d;

        if (value is double v && max > 0)
            return Math.Clamp(v / max, 0, 1);
        if (value is int i && max > 0)
            return Math.Clamp(i / max, 0, 1);

        return 0.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
