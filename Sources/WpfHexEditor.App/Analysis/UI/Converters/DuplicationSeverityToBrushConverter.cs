// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/UI/Converters/DuplicationSeverityToBrushConverter.cs
// Description: Maps DuplicationSeverity (Low/Medium/High) to a frozen semi-
//              transparent brush, used by the Duplication tab severity chip.
// ==========================================================

using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using WpfHexEditor.App.Analysis.Models;

namespace WpfHexEditor.App.Analysis.UI.Converters;

public sealed class DuplicationSeverityToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush LowBrush    = Freeze(new(Color.FromArgb(55, 0x4C, 0xAF, 0x50)));
    private static readonly SolidColorBrush MediumBrush = Freeze(new(Color.FromArgb(55, 0xFF, 0x98, 0x00)));
    private static readonly SolidColorBrush HighBrush   = Freeze(new(Color.FromArgb(85, 0xF4, 0x43, 0x36)));

    public object Convert(object? value, Type _, object? __, CultureInfo ___)
        => value is DuplicationSeverity s
            ? s switch
            {
                DuplicationSeverity.High   => HighBrush,
                DuplicationSeverity.Medium => MediumBrush,
                _                          => LowBrush,
            }
            : Brushes.Transparent;

    public object ConvertBack(object? value, Type _, object? __, CultureInfo ___) => throw new NotSupportedException();

    private static SolidColorBrush Freeze(SolidColorBrush b) { b.Freeze(); return b; }
}
