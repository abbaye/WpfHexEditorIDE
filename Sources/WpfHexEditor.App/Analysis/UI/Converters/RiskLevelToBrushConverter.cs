// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/UI/Converters/RiskLevelToBrushConverter.cs
// Description: Maps LcomLevel (Low/Medium/High) to a semi-transparent colour
//              brush for DataGrid cell backgrounds. Brushes are frozen.
// ==========================================================

using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using WpfHexEditor.App.Analysis.Models;

namespace WpfHexEditor.App.Analysis.UI.Converters;

public sealed class RiskLevelToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush MediumBrush = Freeze(new(Color.FromArgb(55, 0xFF, 0x98, 0x00)));
    private static readonly SolidColorBrush HighBrush   = Freeze(new(Color.FromArgb(55, 0xF4, 0x43, 0x36)));

    public object Convert(object? value, Type _, object? __, CultureInfo ___)
        => value is LcomLevel level
            ? level switch
            {
                LcomLevel.High   => HighBrush,
                LcomLevel.Medium => MediumBrush,
                _                => Brushes.Transparent,
            }
            : Brushes.Transparent;

    public object ConvertBack(object? value, Type _, object? __, CultureInfo ___) => throw new NotSupportedException();

    private static SolidColorBrush Freeze(SolidColorBrush b) { b.Freeze(); return b; }
}
