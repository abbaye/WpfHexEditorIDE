// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/UI/Converters/NullToVisibilityConverter.cs
// Description: Maps any reference value to Visibility. Set Invert=True (or
//              pass "Inverse" as ConverterParameter for legacy call sites)
//              to invert (non-null → Collapsed). Used by the Duplication tab
//              to swap between the "no selection" hint and the preview panel.
// ==========================================================

using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WpfHexEditor.App.Analysis.UI.Converters;

public sealed class NullToVisibilityConverter : IValueConverter
{
    /// <summary>When true, non-null values are Collapsed and null values are Visible.</summary>
    public bool Invert { get; set; }

    public object Convert(object? value, Type _, object? parameter, CultureInfo __)
    {
        bool hasValue = value is not null;
        bool invert   = Invert
                     || (parameter is string s
                      && string.Equals(s, "Inverse", StringComparison.OrdinalIgnoreCase));
        return (hasValue ^ invert) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type _, object? __, CultureInfo ___) => throw new NotSupportedException();
}
