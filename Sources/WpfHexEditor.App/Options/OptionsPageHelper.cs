// ==========================================================
// Project: WpfHexEditor.App
// File: Options/OptionsPageHelper.cs
// Description:
//     Shared factory helpers for code-behind options pages.
//     Produces section headers, hints, and common layout rows
//     using the unified IDE options style (ALL CAPS, FontSize=12,
//     Opt_SectionForegroundBrush dynamic resource).
// ==========================================================

using System.Windows;
using System.Windows.Controls;

namespace WpfHexEditor.App.Options;

internal static class OptionsPageHelper
{
    /// <summary>
    /// Creates an options-page section header: ALL CAPS, FontSize=12, SemiBold,
    /// foreground from <c>Opt_SectionForegroundBrush</c> dynamic resource.
    /// </summary>
    public static TextBlock SectionHeader(string title)
    {
        var tb = new TextBlock
        {
            Text       = title.ToUpperInvariant(),
            FontSize   = 12,
            FontWeight = FontWeights.SemiBold,
            Margin     = new Thickness(0, 16, 0, 6),
        };
        tb.SetResourceReference(TextBlock.ForegroundProperty, "Opt_SectionForegroundBrush");
        return tb;
    }

    /// <summary>
    /// Creates a small italic hint/description line below a control.
    /// </summary>
    public static TextBlock Hint(string text) => new()
    {
        Text      = text,
        Margin    = new Thickness(0, 2, 0, 4),
        FontStyle = FontStyles.Italic,
        Opacity   = 0.6,
    };
}
