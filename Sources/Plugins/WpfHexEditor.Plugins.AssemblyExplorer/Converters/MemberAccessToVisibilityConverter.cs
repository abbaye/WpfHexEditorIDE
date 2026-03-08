// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: Converters/MemberAccessToVisibilityConverter.cs
// Author: Derek Tremblay
// Created: 2026-03-08
// Description:
//     IValueConverter: bool IsPublic → Visibility.
//     Non-public members are shown with reduced opacity (not hidden)
//     so the developer can still see private internals.
// ==========================================================

using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WpfHexEditor.Plugins.AssemblyExplorer.Converters;

/// <summary>
/// Converts a boolean <c>IsPublic</c> value to a <see cref="Visibility"/>.
/// Public → Visible; non-public → Visible (with dimmed icon via a separate opacity binding).
/// Pass "collapse" as ConverterParameter to collapse non-public members instead.
/// </summary>
[ValueConversion(typeof(bool), typeof(Visibility))]
public sealed class MemberAccessToVisibilityConverter : IValueConverter
{
    public static readonly MemberAccessToVisibilityConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var isPublic = value is true;
        var collapse = parameter is string s && s == "collapse";

        if (isPublic) return Visibility.Visible;
        return collapse ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
