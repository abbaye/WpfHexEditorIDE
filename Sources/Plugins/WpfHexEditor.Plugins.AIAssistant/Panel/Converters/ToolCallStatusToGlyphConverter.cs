// ==========================================================
// Project: WpfHexEditor.Plugins.AIAssistant
// File: ToolCallStatusToGlyphConverter.cs
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description: Converts ToolCallStatus to Segoe MDL2 glyph.
// ==========================================================
using System.Globalization;
using System.Windows.Data;
using WpfHexEditor.Plugins.AIAssistant.Panel.Messages;

namespace WpfHexEditor.Plugins.AIAssistant.Panel.Converters;

public sealed class ToolCallStatusToGlyphConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is ToolCallStatus status ? status switch
        {
            ToolCallStatus.Pending => "\uE712",   // More (...)
            ToolCallStatus.Running => "\uE895",    // Sync
            ToolCallStatus.Done => "\uE73E",       // CheckMark
            ToolCallStatus.Error => "\uE711",      // Cancel
            _ => ""
        } : "";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
