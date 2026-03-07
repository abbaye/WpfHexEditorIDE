//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace WpfHexEditor.Terminal;

/// <summary>
/// Converts <see cref="TerminalOutputKind"/> to a <see cref="Brush"/> for RichText display.
/// Colors are theme-independent (error = red, warning = orange, info = blue, standard = theme foreground).
/// </summary>
[ValueConversion(typeof(TerminalOutputKind), typeof(Brush))]
public sealed class OutputKindToBrushConverter : IValueConverter
{
    private static readonly Brush ErrorBrush   = new SolidColorBrush(Color.FromRgb(0xFF, 0x55, 0x55));
    private static readonly Brush WarningBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xBB, 0x33));
    private static readonly Brush InfoBrush    = new SolidColorBrush(Color.FromRgb(0x68, 0xC5, 0xFF));
    private static readonly Brush StandardBrush = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => (TerminalOutputKind)value switch
        {
            TerminalOutputKind.Error   => ErrorBrush,
            TerminalOutputKind.Warning => WarningBrush,
            TerminalOutputKind.Info    => InfoBrush,
            _                          => StandardBrush
        };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
