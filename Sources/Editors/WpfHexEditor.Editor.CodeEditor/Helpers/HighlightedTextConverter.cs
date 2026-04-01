// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Helpers/HighlightedTextConverter.cs
// Description:
//     WPF IValueConverter that builds a TextBlock with matched characters
//     rendered bold+colored for SmartComplete filter highlighting.
// ==========================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;

namespace WpfHexEditor.Editor.CodeEditor.Helpers;

/// <summary>
/// Converts a <see cref="Models.SmartCompleteSuggestion"/> into a <see cref="TextBlock"/>
/// with matched-character positions highlighted in bold.
/// </summary>
[ValueConversion(typeof(Models.SmartCompleteSuggestion), typeof(TextBlock))]
public sealed class HighlightedTextConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not Models.SmartCompleteSuggestion s) return null;

        var tb = new TextBlock
        {
            FontFamily        = new FontFamily("Consolas, Courier New"),
            FontSize          = 12,
            FontWeight        = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        };
        tb.SetResourceReference(TextBlock.ForegroundProperty, "TE_Foreground");

        var text    = s.DisplayText ?? string.Empty;
        var indices = s.MatchedIndices;

        if (indices is null or { Count: 0 })
        {
            tb.Text = text;
            return tb;
        }

        var matchSet  = new HashSet<int>(indices);
        var highlight = Application.Current?.TryFindResource("SC_MatchHighlightBrush") as Brush
                        ?? new SolidColorBrush(Color.FromRgb(24, 163, 255));

        int i = 0;
        while (i < text.Length)
        {
            bool isMatch = matchSet.Contains(i);

            // Collect consecutive chars with same match state
            int start = i;
            while (i < text.Length && matchSet.Contains(i) == isMatch)
                i++;

            var run = new Run(text[start..i]);
            if (isMatch)
            {
                run.FontWeight = FontWeights.Bold;
                run.Foreground = highlight;
            }
            tb.Inlines.Add(run);
        }

        return tb;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
