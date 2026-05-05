// ==========================================================
// Project: WpfHexEditor.App.AssemblyExplorer
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

namespace WpfHexEditor.App.AssemblyExplorer.Converters;

/// <summary>
/// Converts a boolean <c>IsPublic</c> value to a <see cref="Visibility"/>.
/// Supports three modes via ConverterParameter:
/// <list type="bullet">
///   <item><c>null</c> — always Visible (both public and non-public shown)</item>
///   <item><c>"collapse"</c> — public=Visible, non-public=Collapsed</item>
///   <item><c>"invert"</c> — public=Collapsed, non-public=Visible (use for lock icon)</item>
/// </list>
/// </summary>
[ValueConversion(typeof(bool), typeof(Visibility))]
public sealed class MemberAccessToVisibilityConverter : IValueConverter
{
    public static readonly MemberAccessToVisibilityConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var isPublic = value is true;

        return parameter is string mode ? mode switch
        {
            "collapse" => isPublic ? Visibility.Visible   : Visibility.Collapsed,
            "invert"   => isPublic ? Visibility.Collapsed : Visibility.Visible,
            _          => Visibility.Visible
        } : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
