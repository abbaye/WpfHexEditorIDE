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
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string kind) return DependencyProperty.UnsetValue;

        var resourceKey = kind.ToLowerInvariant() switch
        {
            "class" or "struct" or "record" or "object" => "DS_ClassIconBrush",
            "method" or "function" or "constructor"     => "DS_MethodIconBrush",
            "property"                                  => "DS_PropertyIconBrush",
            "field" or "variable"                       => "DS_FieldIconBrush",
            "enum" or "enummember"                      => "DS_EnumIconBrush",
            "interface"                                 => "DS_InterfaceIconBrush",
            _                                           => "DS_NodeForeground",
        };

        return Application.Current.TryFindResource(resourceKey) is Brush brush
            ? brush
            : Brushes.Gray;
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

/// <summary>Converts null to Collapsed, non-null to Visible.</summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        => value is null ? Visibility.Collapsed : Visibility.Visible;

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
