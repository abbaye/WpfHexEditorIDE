// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/UI/Converters/FileNameConverter.cs
// ==========================================================

using System.Globalization;
using System.IO;
using System.Windows.Data;

namespace WpfHexEditor.App.Analysis.UI.Converters;

public sealed class FileNameConverter : IValueConverter
{
    public static readonly FileNameConverter Instance = new();

    public object Convert(object? value, Type _, object? __, CultureInfo ___)
        => value is string s ? Path.GetFileName(s) : string.Empty;

    public object ConvertBack(object? value, Type _, object? __, CultureInfo ___) => throw new NotSupportedException();
}
