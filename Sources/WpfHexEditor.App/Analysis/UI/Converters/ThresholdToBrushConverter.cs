// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/UI/Converters/ThresholdToBrushConverter.cs
// Description: Maps a numeric value to a semi-transparent colour brush using
//              a threshold string: "v1:#RRGGBB;v2:#RRGGBB;v3:#RRGGBB"
//              Returns Transparent when value is below the first threshold.
// ==========================================================

using System.Collections.Concurrent;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace WpfHexEditor.App.Analysis.UI.Converters;

/// <summary>
/// ConverterParameter format: "t1:#RRGGBB;t2:#RRGGBB;t3:#RRGGBB" (ascending thresholds).
/// Value below t1 → Transparent. Parsed entries are cached and brushes frozen per spec string.
/// </summary>
public sealed class ThresholdToBrushConverter : IValueConverter
{
    private const byte Alpha = 55;

    // Keyed by spec string — compile-time constants in XAML, so bounded and small.
    private static readonly ConcurrentDictionary<string, (double threshold, SolidColorBrush brush)[]> _cache = new();

    public object Convert(object? value, Type _, object? parameter, CultureInfo __)
    {
        if (parameter is not string spec || string.IsNullOrEmpty(spec))
            return Brushes.Transparent;

        if (!TryParseDouble(value, out double d))
            return Brushes.Transparent;

        var entries = _cache.GetOrAdd(spec, ParseAndCache);

        // Entries are ascending — walk forward, last entry ≥ d wins.
        SolidColorBrush? result = null;
        foreach (var (threshold, brush) in entries)
        {
            if (d < threshold) break;
            result = brush;
        }
        return result ?? Brushes.Transparent;
    }

    public object ConvertBack(object? value, Type _, object? __, CultureInfo ___) => throw new NotSupportedException();

    private static bool TryParseDouble(object? value, out double d)
    {
        d = 0;
        if (value is null) return false;
        return double.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out d);
    }

    private static (double, SolidColorBrush)[] ParseAndCache(string spec)
    {
        var list = new List<(double, SolidColorBrush)>();
        foreach (var part in spec.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split(':');
            if (kv.Length != 2) continue;
            if (!double.TryParse(kv[0].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double t)) continue;
            var color = ParseHex(kv[1].Trim());
            if (color is null) continue;
            var c = color.Value;
            var brush = new SolidColorBrush(Color.FromArgb(Alpha, c.R, c.G, c.B));
            brush.Freeze();
            list.Add((t, brush));
        }
        return [.. list];
    }

    private static Color? ParseHex(string hex)
    {
        try { return (Color)ColorConverter.ConvertFromString(hex); }
        catch { return null; }
    }
}
