// ==========================================================
// Project: WpfHexEditor.Plugins.AIAssistant
// File: ToolCallStatusToBrushConverter.cs
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description: Converts ToolCallStatus to a themed brush via DynamicResource lookup.
// ==========================================================
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using WpfHexEditor.Plugins.AIAssistant.Panel.Messages;

namespace WpfHexEditor.Plugins.AIAssistant.Panel.Converters;

public sealed class ToolCallStatusToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var key = value is ToolCallStatus status ? status switch
        {
            ToolCallStatus.Done => "CA_TitleBarBadgeIdleBrush",
            ToolCallStatus.Error => "CA_ErrorBrush",
            ToolCallStatus.Running => "CA_TitleBarBadgeStreamingBrush",
            _ => "CA_ToolCallForegroundBrush"
        } : "CA_ToolCallForegroundBrush";

        return Application.Current.TryFindResource(key) as Brush ?? Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
