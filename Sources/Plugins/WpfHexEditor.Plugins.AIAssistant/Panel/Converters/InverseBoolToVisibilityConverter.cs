// ==========================================================
// Project: WpfHexEditor.Plugins.AIAssistant
// File: InverseBoolToVisibilityConverter.cs
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description: Converts false→Visible, true→Collapsed.
// ==========================================================
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WpfHexEditor.Plugins.AIAssistant.Panel.Converters;

public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
