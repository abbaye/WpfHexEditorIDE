//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WpfHexEditor.Plugins.ParsedFields.Views;

/// <summary>
/// Converts a group name to an IsExpanded bool.
/// Groups listed in the ConverterParameter (comma-separated) start collapsed.
/// Default collapsed groups: "Format Metadata"
/// </summary>
public class GroupNameToExpandedConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string groupName) return true;

        string collapsed = parameter as string ?? "Format Metadata";
        foreach (var entry in collapsed.Split(','))
            if (string.Equals(groupName, entry.Trim(), StringComparison.Ordinal))
                return false;

        return true;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>
/// Converts bool to inverse Visibility (true=Collapsed, false=Visible).
/// </summary>
public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
            return b ? Visibility.Collapsed : Visibility.Visible;
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>Maps IsAiSectionExpanded bool to Segoe MDL2 chevron glyph.</summary>
public class BoolToChevronConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? "" : ""; // chevron-down : chevron-right

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}
