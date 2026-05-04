// ==========================================================
// Project: WpfHexEditor.Plugins.Debugger
// File: Visualizers/DateTimeVisualizer.cs
// Description: Debug visualizer for DateTime / DateTimeOffset / TimeSpan values.
//              Parses the raw DAP string and displays formatted date/time fields.
// Architecture: IDebugVisualizer implementation.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.SDK.Contracts;

namespace WpfHexEditor.Plugins.Debugger.Visualizers;

internal sealed class DateTimeVisualizer : IDebugVisualizer
{
    private static readonly HashSet<string> _supportedTypes =
    [
        "System.DateTime", "DateTime",
        "System.DateTimeOffset", "DateTimeOffset",
        "System.TimeSpan", "TimeSpan",
    ];

    public string DisplayName => "DateTime Visualizer";

    public bool CanVisualize(DebugVariableContext context)
        => _supportedTypes.Contains(context.TypeName);

    public FrameworkElement CreateView(DebugVariableContext context)
    {
        var grid = new Grid { Margin = new Thickness(8) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var fields = ParseFields(context.TypeName, context.RawValue);

        for (int i = 0; i < fields.Count; i++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var label = new TextBlock
            {
                Text       = fields[i].Label + ":",
                FontWeight = FontWeights.SemiBold,
                Margin     = new Thickness(0, 2, 8, 2),
                VerticalAlignment = VerticalAlignment.Center,
            };
            label.SetResourceReference(TextBlock.ForegroundProperty, "DockMenuForegroundBrush");
            Grid.SetRow(label, i); Grid.SetColumn(label, 0);

            var value = new TextBlock
            {
                Text   = fields[i].Value,
                Margin = new Thickness(0, 2, 0, 2),
                VerticalAlignment = VerticalAlignment.Center,
            };
            value.SetResourceReference(TextBlock.ForegroundProperty, "DockMenuForegroundBrush");
            Grid.SetRow(value, i); Grid.SetColumn(value, 1);

            grid.Children.Add(label);
            grid.Children.Add(value);
        }

        return new Border { Padding = new Thickness(4), MinWidth = 280, Child = grid };
    }

    private static List<(string Label, string Value)> ParseFields(string typeName, string raw)
    {
        var result = new List<(string, string)>();

        // Attempt to parse DateTime / DateTimeOffset
        if (DateTime.TryParse(raw.Trim('"'), System.Globalization.CultureInfo.InvariantCulture,
                              System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
        {
            result.Add(("Raw",        raw.Trim('"')));
            result.Add(("Date",       dt.ToString("yyyy-MM-dd")));
            result.Add(("Time",       dt.ToString("HH:mm:ss.fff")));
            result.Add(("Day of week", dt.DayOfWeek.ToString()));
            result.Add(("Kind",       dt.Kind.ToString()));
            result.Add(("Ticks",      dt.Ticks.ToString("N0")));
            return result;
        }

        // TimeSpan
        if (TimeSpan.TryParse(raw.Trim('"'), out var ts))
        {
            result.Add(("Raw",          raw.Trim('"')));
            result.Add(("Days",         ts.Days.ToString()));
            result.Add(("Hours",        ts.Hours.ToString()));
            result.Add(("Minutes",      ts.Minutes.ToString()));
            result.Add(("Seconds",      ts.Seconds.ToString()));
            result.Add(("Milliseconds", ts.Milliseconds.ToString()));
            result.Add(("Total seconds", ts.TotalSeconds.ToString("N3")));
            return result;
        }

        // Fallback — show raw value
        result.Add(("Value", raw));
        return result;
    }
}
