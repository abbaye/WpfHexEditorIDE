//////////////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project: WpfHexEditor.Editor.StructureEditor
// File: Controls/SeverityToBrushConverter.cs
// Description: IValueConverter mapping ValidationSeverity enum to a SolidColorBrush.
//////////////////////////////////////////////////////

using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using WpfHexEditor.Editor.Core.Validation;

namespace WpfHexEditor.Editor.StructureEditor.Controls;

[ValueConversion(typeof(ValidationSeverity), typeof(Brush))]
internal sealed class SeverityToBrushConverter : IValueConverter
{
    public static readonly SeverityToBrushConverter Instance = new();

    private static readonly SolidColorBrush ErrorBrush   = new(Color.FromRgb(0xFF, 0x4C, 0x4C));
    private static readonly SolidColorBrush WarningBrush = new(Color.FromRgb(0xFF, 0xA5, 0x00));
    private static readonly SolidColorBrush InfoBrush    = new(Color.FromRgb(0x88, 0xBB, 0xFF));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is ValidationSeverity sev ? sev switch
        {
            ValidationSeverity.Error   => ErrorBrush,
            ValidationSeverity.Warning => WarningBrush,
            _                          => InfoBrush,
        } : InfoBrush;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
