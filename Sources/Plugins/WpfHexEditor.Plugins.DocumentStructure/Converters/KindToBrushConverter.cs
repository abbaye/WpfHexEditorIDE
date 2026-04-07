// ==========================================================
// Project: WpfHexEditor.Plugins.DocumentStructure
// File: Converters/KindToBrushConverter.cs
// Created: 2026-04-05
// Description:
//     IValueConverter mapping Kind strings to themed SolidColorBrush resources.
//     Falls back to DS_NodeForeground when no specific token exists.
// ==========================================================

using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace WpfHexEditor.Plugins.DocumentStructure.Converters;

public sealed class KindToBrushConverter : IValueConverter
{
    // Hardcoded palette — identical to CodeEditor NavigationBar (NavigationBarItem.IconBrush).
    private static readonly Dictionary<string, SolidColorBrush> KindBrushMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["namespace"]     = Freeze("#DCDCAA"),
            ["module"]        = Freeze("#DCDCAA"),
            ["class"]         = Freeze("#4FC1FF"),
            ["record"]        = Freeze("#4FC1FF"),
            ["object"]        = Freeze("#4FC1FF"),
            ["interface"]     = Freeze("#B8D7A3"),
            ["struct"]        = Freeze("#4EC9B0"),
            ["enum"]          = Freeze("#CE9178"),
            ["enummember"]    = Freeze("#CE9178"),
            ["delegate"]      = Freeze("#C586C0"),
            ["method"]        = Freeze("#C586C0"),
            ["function"]      = Freeze("#C586C0"),
            ["constructor"]   = Freeze("#C586C0"),
            ["property"]      = Freeze("#9CDCFE"),
            ["indexer"]       = Freeze("#9CDCFE"),
            ["field"]         = Freeze("#9CDCFE"),
            ["variable"]      = Freeze("#9CDCFE"),
            ["constant"]      = Freeze("#9CDCFE"),
            ["typeparameter"] = Freeze("#9CDCFE"),
            ["event"]         = Freeze("#DCDCAA"),
        };

    private static SolidColorBrush Freeze(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        b.Freeze();
        return b;
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string kind) return DependencyProperty.UnsetValue;
        return KindBrushMap.TryGetValue(kind, out var brush) ? brush : Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Converts bool false to Visible, true to Collapsed (inverse of BooleanToVisibilityConverter).</summary>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Converts an IndentLevel int to a Thickness (left margin = level * 16).</summary>
public sealed class IndentToMarginConverter : MarkupExtensionValueConverter
{
    public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is int indent ? new Thickness(indent * 16, 1, 0, 1) : new Thickness(0, 1, 0, 1);

    public override object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Converts null or empty string to Collapsed, any other value to Visible.</summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        => value is null || (value is string s && s.Length == 0) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Base class for converters that can be used directly as markup extensions.
/// </summary>
public abstract class MarkupExtensionValueConverter : System.Windows.Markup.MarkupExtension, IValueConverter
{
    public override object ProvideValue(IServiceProvider serviceProvider) => this;
    public abstract object Convert(object value, Type targetType, object parameter, CultureInfo culture);
    public abstract object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture);
}
