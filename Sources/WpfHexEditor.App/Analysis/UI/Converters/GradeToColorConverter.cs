// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/UI/Converters/GradeToColorConverter.cs
// ==========================================================

using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace WpfHexEditor.App.Analysis.UI.Converters;

public sealed class GradeToColorConverter : IValueConverter
{
    public object Convert(object? value, Type _, object? __, CultureInfo ___)
    {
        return (value?.ToString() ?? "F") switch
        {
            "A+" or "A" or "A-" => new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)),
            "B+" or "B" or "B-" => new SolidColorBrush(Color.FromRgb(0x81, 0xC7, 0x84)),
            "C+" or "C" or "C-" => new SolidColorBrush(Color.FromRgb(0xFF, 0xB3, 0x00)),
            "D"                  => new SolidColorBrush(Color.FromRgb(0xFF, 0x72, 0x00)),
            _                    => new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36)),
        };
    }

    public object ConvertBack(object? value, Type _, object? __, CultureInfo ___) => throw new NotSupportedException();
}
