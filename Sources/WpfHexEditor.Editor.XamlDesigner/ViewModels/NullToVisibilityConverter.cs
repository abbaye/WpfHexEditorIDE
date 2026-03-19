// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: NullToVisibilityConverter.cs
// Author: Derek Tremblay
// Created: 2026-03-17
// Description: Utility IValueConverter — null → Collapsed, non-null → Visible.
// ==========================================================

using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WpfHexEditor.Editor.XamlDesigner.ViewModels;

[System.Windows.Markup.ValueSerializer(typeof(object))]
public sealed class NullToVisibilityConverter : IValueConverter
{
    public static readonly NullToVisibilityConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is null ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
