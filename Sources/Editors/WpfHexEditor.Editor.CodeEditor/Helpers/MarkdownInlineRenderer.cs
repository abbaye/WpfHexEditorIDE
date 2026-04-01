// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Helpers/MarkdownInlineRenderer.cs
// Description:
//     Converts a markdown string to WPF FrameworkElements for display
//     in QuickInfoPopup and other documentation areas.
//     Handles: bold, italic, inline code, fenced code blocks, lists, hyperlinks.
//
// Architecture Notes:
//     Pure static helper — no state, no WPF dependency tree.
//     Each public method returns UIElement-derived objects ready for
//     insertion into a StackPanel.
//     Theme tokens CE_InlineCodeBackground and CE_CodeBlockBackground
//     are resolved from Application.Current.
// ==========================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace WpfHexEditor.Editor.CodeEditor.Helpers;

/// <summary>
/// Converts markdown text to WPF UI elements (TextBlock per paragraph).
/// </summary>
internal static class MarkdownInlineRenderer
{
    // ── Regex patterns ────────────────────────────────────────────────────────

    private static readonly Regex s_fencedBlock =
        new(@"```[a-zA-Z]*\r?\n(.*?)\r?\n```", RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex s_link =
        new(@"\[([^\]]+)\]\(([^)]+)\)", RegexOptions.Compiled);

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Converts <paramref name="markdown"/> into a list of UIElements
    /// (TextBlock / Border) ready for insertion into a StackPanel.
    /// </summary>
    public static IReadOnlyList<UIElement> Render(string markdown)
    {
        var elements = new List<UIElement>();

        // Process fenced code blocks first (multi-line)
        int pos = 0;
        foreach (Match m in s_fencedBlock.Matches(markdown))
        {
            // Render any text before this block
            if (m.Index > pos)
                RenderParagraphs(markdown[pos..m.Index], elements);

            elements.Add(BuildCodeBlock(m.Groups[1].Value));
            pos = m.Index + m.Length;
        }

        // Render remaining text
        if (pos < markdown.Length)
            RenderParagraphs(markdown[pos..], elements);

        return elements;
    }

    // ── Paragraph renderer ────────────────────────────────────────────────────

    private static void RenderParagraphs(string text, List<UIElement> elements)
    {
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line)) continue;

            // List item
            if (line.StartsWith("- ", StringComparison.Ordinal)
                || line.StartsWith("* ", StringComparison.Ordinal))
            {
                var tb = BuildInlineTextBlock("• " + line[2..]);
                tb.Margin = new Thickness(4, 1, 0, 1);
                elements.Add(tb);
            }
            else
            {
                var tb = BuildInlineTextBlock(line);
                elements.Add(tb);
            }
        }
    }

    // ── Inline TextBlock builder ──────────────────────────────────────────────

    /// <summary>
    /// Creates a WPF <see cref="TextBlock"/> with bold, italic, inline-code,
    /// and hyperlink spans parsed from <paramref name="markdown"/>.
    /// </summary>
    public static TextBlock BuildInlineTextBlock(string markdown)
    {
        var tb = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize     = 12,
            Margin       = new Thickness(0, 0, 0, 2),
        };
        tb.SetResourceReference(TextBlock.ForegroundProperty, "CE_QuickInfo_TypeText");

        foreach (var inline in ParseInlines(markdown))
            tb.Inlines.Add(inline);

        return tb;
    }

    // ── Inline parser ─────────────────────────────────────────────────────────

    private static IEnumerable<Inline> ParseInlines(string text)
    {
        int i = 0;
        var sb = new System.Text.StringBuilder();

        while (i < text.Length)
        {
            // Bold: **text** or __text__
            if (TryMatch(text, i, "**", out int end) || TryMatch(text, i, "__", out end))
            {
                if (sb.Length > 0) { yield return new Run(sb.ToString()); sb.Clear(); }
                var inner = text[(i + 2)..end];
                yield return new Bold(new Run(inner));
                i = end + 2;
                continue;
            }

            // Italic: *text* or _text_ (single, not double)
            if (TryMatchItalic(text, i, out end))
            {
                if (sb.Length > 0) { yield return new Run(sb.ToString()); sb.Clear(); }
                var inner = text[(i + 1)..end];
                yield return new Italic(new Run(inner));
                i = end + 1;
                continue;
            }

            // Inline code: `code`
            if (text[i] == '`')
            {
                var close = text.IndexOf('`', i + 1);
                if (close > i)
                {
                    if (sb.Length > 0) { yield return new Run(sb.ToString()); sb.Clear(); }
                    yield return BuildInlineCode(text[(i + 1)..close]);
                    i = close + 1;
                    continue;
                }
            }

            // Hyperlink: [text](url)
            if (text[i] == '[')
            {
                var lm = s_link.Match(text, i);
                if (lm.Success && lm.Index == i)
                {
                    if (sb.Length > 0) { yield return new Run(sb.ToString()); sb.Clear(); }
                    yield return BuildHyperlink(lm.Groups[1].Value, lm.Groups[2].Value);
                    i += lm.Length;
                    continue;
                }
            }

            sb.Append(text[i++]);
        }

        if (sb.Length > 0) yield return new Run(sb.ToString());
    }

    private static bool TryMatch(string text, int start, string delim, out int closeStart)
    {
        closeStart = -1;
        if (start + delim.Length > text.Length) return false;
        if (text[start..(start + delim.Length)] != delim) return false;

        var close = text.IndexOf(delim, start + delim.Length, StringComparison.Ordinal);
        if (close <= start) return false;

        closeStart = close;
        return true;
    }

    private static bool TryMatchItalic(string text, int start, out int closeStart)
    {
        var ch = text[start];
        if (ch != '*' && ch != '_') { closeStart = -1; return false; }

        // Ensure it's a single marker (not double)
        if (start + 1 < text.Length && text[start + 1] == ch) { closeStart = -1; return false; }

        var close = text.IndexOf(ch, start + 1);
        if (close > start + 1 && (close + 1 >= text.Length || text[close + 1] != ch))
        {
            closeStart = close;
            return true;
        }
        closeStart = -1;
        return false;
    }

    // ── Inline code span ──────────────────────────────────────────────────────

    private static Inline BuildInlineCode(string code)
    {
        var bg = Application.Current?.TryFindResource("CE_InlineCodeBackground") as Brush
                 ?? new SolidColorBrush(Color.FromRgb(45, 45, 45));

        var tb = new TextBlock
        {
            Text           = code,
            FontFamily     = new FontFamily("Consolas, Courier New"),
            FontSize       = 11,
            Background     = bg,
            Padding        = new Thickness(2, 0, 2, 0),
        };
        tb.SetResourceReference(TextBlock.ForegroundProperty, "CE_QuickInfo_TypeText");

        return new InlineUIContainer(tb) { BaselineAlignment = BaselineAlignment.Center };
    }

    // ── Hyperlink ─────────────────────────────────────────────────────────────

    private static Inline BuildHyperlink(string text, string url)
    {
        var link = new Hyperlink(new Run(text));
        try { link.NavigateUri = new Uri(url); } catch { }
        link.RequestNavigate += (_, e) =>
        {
            try { Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); }
            catch { }
            e.Handled = true;
        };
        return link;
    }

    // ── Fenced code block ─────────────────────────────────────────────────────

    private static UIElement BuildCodeBlock(string code)
    {
        var bg = Application.Current?.TryFindResource("CE_CodeBlockBackground") as Brush
                 ?? new SolidColorBrush(Color.FromRgb(37, 37, 38));
        var fg = Application.Current?.TryFindResource("CE_QuickInfo_TypeText") as Brush
                 ?? Brushes.LightGray;

        return new Border
        {
            Background      = bg,
            BorderThickness = new Thickness(0),
            CornerRadius    = new CornerRadius(2),
            Padding         = new Thickness(6, 4, 6, 4),
            Margin          = new Thickness(0, 2, 0, 2),
            Child           = new TextBlock
            {
                Text       = code.Trim(),
                FontFamily = new FontFamily("Consolas, Courier New"),
                FontSize   = 11,
                Foreground = fg,
                TextWrapping = TextWrapping.Wrap,
            },
        };
    }
}
