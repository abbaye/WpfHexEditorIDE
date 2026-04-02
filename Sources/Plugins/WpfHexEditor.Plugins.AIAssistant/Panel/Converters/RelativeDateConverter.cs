// ==========================================================
// Project: WpfHexEditor.Plugins.AIAssistant
// File: RelativeDateConverter.cs
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description: Converts DateTime to relative human-readable string.
// ==========================================================
using System.Globalization;
using System.Windows.Data;

namespace WpfHexEditor.Plugins.AIAssistant.Panel.Converters;

public sealed class RelativeDateConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not DateTime dt || dt == default) return "";
        var span = DateTime.Now - dt;

        return span.TotalSeconds switch
        {
            < 60 => "just now",
            < 3600 => $"{(int)span.TotalMinutes} min ago",
            < 86400 when dt.Date == DateTime.Today => $"{(int)span.TotalHours}h ago",
            < 172800 when dt.Date == DateTime.Today.AddDays(-1) => "yesterday",
            _ => dt.ToString("MMM d", CultureInfo.InvariantCulture)
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
