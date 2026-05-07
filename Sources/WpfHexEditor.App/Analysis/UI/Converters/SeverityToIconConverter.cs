// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/UI/Converters/SeverityToIconConverter.cs
// ==========================================================

using System.Globalization;
using System.Windows.Data;
using WpfHexEditor.App.Analysis.Models;

namespace WpfHexEditor.App.Analysis.UI.Converters;

public sealed class SeverityToIconConverter : IValueConverter
{
    public object Convert(object? value, Type _, object? __, CultureInfo ___)
        => value is DiagnosticSeverity s ? s switch
        {
            DiagnosticSeverity.Error   => "●",
            DiagnosticSeverity.Warning => "◑",
            _                          => "○",
        } : "○";

    public object ConvertBack(object? value, Type _, object? __, CultureInfo ___) => throw new NotSupportedException();
}
