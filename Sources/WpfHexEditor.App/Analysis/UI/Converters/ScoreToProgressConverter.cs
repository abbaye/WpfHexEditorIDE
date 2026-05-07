// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/UI/Converters/ScoreToProgressConverter.cs
// ==========================================================

using System.Globalization;
using System.Windows.Data;

namespace WpfHexEditor.App.Analysis.UI.Converters;

public sealed class ScoreToProgressConverter : IValueConverter
{
    public object Convert(object? value, Type _, object? __, CultureInfo ___)
        => value is int i ? i / 100.0 : 0.0;

    public object ConvertBack(object? value, Type _, object? __, CultureInfo ___) => throw new NotSupportedException();
}
