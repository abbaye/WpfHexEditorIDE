// ==========================================================
// Project: WpfHexEditor.Plugins.AIAssistant
// File: ChatMarkdownRenderer.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-04-01
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Converts markdown text to WPF UIElements for rich chat display.
//     Handles: bold, italic, inline code, fenced code blocks with syntax
//     highlighting, headings, lists, hyperlinks, bare URLs, file paths.
// ==========================================================
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace WpfHexEditor.Plugins.AIAssistant.Panel.Messages;

internal static class ChatMarkdownRenderer
{
    // ── Regex patterns ───────────────────────────────────────────────────

    private static readonly Regex s_fencedBlock =
        new(@"```(\w*)\r?\n(.*?)\r?\n```", RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex s_mdLink =
        new(@"\[([^\]]+)\]\(([^)]+)\)", RegexOptions.Compiled);

    private static readonly Regex s_bareUrl =
        new(@"(?<!\[.*?\]\()https?://[^\s""'<>\)]+", RegexOptions.Compiled);

    private static readonly Regex s_filePath =
        new(@"(?<![""'\w])([A-Z]:\\[\w\\.\-]+\.\w{1,10})", RegexOptions.Compiled);

    /// <summary>Event raised when a file path is clicked in a message.</summary>
    public static event Action<string>? FilePathClicked;

    // ── Public API ───────────────────────────────────────────────────────

    public static IReadOnlyList<UIElement> Render(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return [BuildParagraph("")];

        var elements = new List<UIElement>();
        int pos = 0;

        foreach (Match m in s_fencedBlock.Matches(markdown))
        {
            if (m.Index > pos)
                RenderParagraphs(markdown[pos..m.Index], elements);

            var lang = m.Groups[1].Value;
            var code = m.Groups[2].Value;
            elements.Add(BuildCodeBlock(code, string.IsNullOrEmpty(lang) ? null : lang));
            pos = m.Index + m.Length;
        }

        if (pos < markdown.Length)
            RenderParagraphs(markdown[pos..], elements);

        return elements;
    }

    // ── Paragraph renderer ───────────────────────────────────────────────

    private static void RenderParagraphs(string text, List<UIElement> elements)
    {
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line)) continue;

            // Heading: ## text
            if (line.StartsWith("### ", StringComparison.Ordinal))
            {
                var tb = BuildInlineTextBlock(line[4..]);
                tb.FontSize = 13;
                tb.FontWeight = FontWeights.SemiBold;
                tb.Margin = new Thickness(0, 6, 0, 2);
                elements.Add(tb);
            }
            else if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                var tb = BuildInlineTextBlock(line[3..]);
                tb.FontSize = 14;
                tb.FontWeight = FontWeights.Bold;
                tb.Margin = new Thickness(0, 8, 0, 2);
                elements.Add(tb);
            }
            else if (line.StartsWith("# ", StringComparison.Ordinal))
            {
                var tb = BuildInlineTextBlock(line[2..]);
                tb.FontSize = 15;
                tb.FontWeight = FontWeights.Bold;
                tb.Margin = new Thickness(0, 10, 0, 4);
                elements.Add(tb);
            }
            // Unordered list
            else if (line.StartsWith("- ", StringComparison.Ordinal) ||
                     line.StartsWith("* ", StringComparison.Ordinal))
            {
                var tb = BuildInlineTextBlock("  \u2022 " + line[2..]);
                tb.Margin = new Thickness(4, 1, 0, 1);
                elements.Add(tb);
            }
            // Ordered list
            else if (line.Length > 2 && char.IsDigit(line[0]) && line.IndexOf(". ", StringComparison.Ordinal) is > 0 and int dotIdx && dotIdx < 4)
            {
                var tb = BuildInlineTextBlock("  " + line);
                tb.Margin = new Thickness(4, 1, 0, 1);
                elements.Add(tb);
            }
            else
            {
                elements.Add(BuildInlineTextBlock(line));
            }
        }
    }

    // ── Inline TextBlock builder ─────────────────────────────────────────

    private static TextBlock BuildParagraph(string text)
    {
        var tb = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12.5,
        };
        tb.SetResourceReference(TextBlock.ForegroundProperty, "CA_MessageForegroundBrush");
        return tb;
    }

    private static TextBlock BuildInlineTextBlock(string markdown)
    {
        var tb = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12.5,
            Margin = new Thickness(0, 0, 0, 2),
        };
        tb.SetResourceReference(TextBlock.ForegroundProperty, "CA_MessageForegroundBrush");

        foreach (var inline in ParseInlines(markdown))
            tb.Inlines.Add(inline);

        return tb;
    }

    // ── Inline parser ────────────────────────────────────────────────────

    private static IEnumerable<Inline> ParseInlines(string text)
    {
        int i = 0;
        var sb = new StringBuilder();

        while (i < text.Length)
        {
            // Bold: **text**
            if (TryMatchDelim(text, i, "**", out int end))
            {
                if (sb.Length > 0) { foreach (var r in EmitTextWithLinks(sb.ToString())) yield return r; sb.Clear(); }
                var inner = text[(i + 2)..end];
                yield return new Bold(new Run(inner));
                i = end + 2;
                continue;
            }

            // Italic: *text* (single, not double)
            if (TryMatchItalic(text, i, out end))
            {
                if (sb.Length > 0) { foreach (var r in EmitTextWithLinks(sb.ToString())) yield return r; sb.Clear(); }
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
                    if (sb.Length > 0) { foreach (var r in EmitTextWithLinks(sb.ToString())) yield return r; sb.Clear(); }
                    yield return BuildInlineCode(text[(i + 1)..close]);
                    i = close + 1;
                    continue;
                }
            }

            // Markdown link: [text](url)
            if (text[i] == '[')
            {
                var lm = s_mdLink.Match(text, i);
                if (lm.Success && lm.Index == i)
                {
                    if (sb.Length > 0) { foreach (var r in EmitTextWithLinks(sb.ToString())) yield return r; sb.Clear(); }
                    yield return BuildHyperlink(lm.Groups[1].Value, lm.Groups[2].Value);
                    i += lm.Length;
                    continue;
                }
            }

            sb.Append(text[i++]);
        }

        if (sb.Length > 0)
            foreach (var r in EmitTextWithLinks(sb.ToString())) yield return r;
    }

    /// <summary>Emits Runs with bare URLs and file paths converted to Hyperlinks.</summary>
    private static IEnumerable<Inline> EmitTextWithLinks(string text)
    {
        // Merge URL and file path matches, process in order
        var matches = new List<(int Index, int Length, string Url, string Display, bool IsFile)>();

        foreach (Match m in s_bareUrl.Matches(text))
            matches.Add((m.Index, m.Length, m.Value, m.Value, false));
        foreach (Match m in s_filePath.Matches(text))
        {
            // Skip if overlapping with a URL match
            if (matches.Any(u => m.Index >= u.Index && m.Index < u.Index + u.Length))
                continue;
            matches.Add((m.Index, m.Length, m.Value, m.Value, true));
        }

        matches.Sort((a, b) => a.Index.CompareTo(b.Index));

        int pos = 0;
        foreach (var (idx, len, url, display, isFile) in matches)
        {
            if (idx > pos)
                yield return new Run(text[pos..idx]);

            if (isFile)
                yield return BuildFileLink(display);
            else
                yield return BuildHyperlink(display, url);

            pos = idx + len;
        }

        if (pos < text.Length)
            yield return new Run(text[pos..]);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static bool TryMatchDelim(string text, int start, string delim, out int closeStart)
    {
        closeStart = -1;
        if (start + delim.Length >= text.Length) return false;
        if (text[start..(start + delim.Length)] != delim) return false;

        var close = text.IndexOf(delim, start + delim.Length, StringComparison.Ordinal);
        if (close <= start) return false;

        closeStart = close;
        return true;
    }

    private static bool TryMatchItalic(string text, int start, out int closeStart)
    {
        closeStart = -1;
        var ch = text[start];
        if (ch != '*' && ch != '_') return false;
        if (start + 1 < text.Length && text[start + 1] == ch) return false;

        var close = text.IndexOf(ch, start + 1);
        if (close > start + 1 && (close + 1 >= text.Length || text[close + 1] != ch))
        {
            closeStart = close;
            return true;
        }
        return false;
    }

    // ── Inline code ──────────────────────────────────────────────────────

    private static Inline BuildInlineCode(string code)
    {
        var tb = new TextBlock
        {
            Text = code,
            FontFamily = new FontFamily("Cascadia Code,Consolas,Courier New"),
            FontSize = 11.5,
            Padding = new Thickness(4, 1, 4, 1),
        };
        tb.SetResourceReference(TextBlock.BackgroundProperty, "CA_CodeBlockBackgroundBrush");
        tb.SetResourceReference(TextBlock.ForegroundProperty, "CA_MessageForegroundBrush");

        return new InlineUIContainer(tb) { BaselineAlignment = BaselineAlignment.Center };
    }

    // ── Hyperlink ────────────────────────────────────────────────────────

    private static Inline BuildHyperlink(string text, string url)
    {
        var run = new Run(text);
        var link = new Hyperlink(run);
        link.SetResourceReference(Hyperlink.ForegroundProperty, "CA_AccentBrandingBrush");
        link.TextDecorations = null;

        try { link.NavigateUri = new Uri(url); }
        catch { return new Run(text); }

        link.RequestNavigate += (_, e) =>
        {
            try { Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); }
            catch { }
            e.Handled = true;
        };

        return link;
    }

    private static Inline BuildFileLink(string path)
    {
        var run = new Run(path);
        var link = new Hyperlink(run)
        {
            Cursor = Cursors.Hand,
            TextDecorations = TextDecorations.Underline,
        };
        link.SetResourceReference(Hyperlink.ForegroundProperty, "CA_AccentBrandingBrush");
        link.Click += (_, _) => FilePathClicked?.Invoke(path);
        return link;
    }

    // ── Fenced code block ────────────────────────────────────────────────

    private static UIElement BuildCodeBlock(string code, string? language)
    {
        var outerBorder = new Border
        {
            CornerRadius = new CornerRadius(6),
            Margin = new Thickness(0, 4, 0, 4),
            BorderThickness = new Thickness(1),
        };
        outerBorder.SetResourceReference(Border.BackgroundProperty, "CA_CodeBlockBackgroundBrush");
        outerBorder.SetResourceReference(Border.BorderBrushProperty, "DockBorderBrush");

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // ── Header row: language + copy button ───────────────────────────
        var header = new DockPanel { Margin = new Thickness(10, 6, 6, 4) };

        if (!string.IsNullOrEmpty(language))
        {
            var langLabel = new TextBlock
            {
                Text = language,
                FontSize = 10.5,
                Opacity = 0.6,
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = new FontFamily("Cascadia Code,Consolas,Courier New"),
            };
            langLabel.SetResourceReference(TextBlock.ForegroundProperty, "CA_MessageForegroundBrush");
            header.Children.Add(langLabel);
        }

        var trimmedCode = code.Trim();

        var copyBtn = new Button
        {
            Content = "\uE8C8",
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 12,
            Width = 28, Height = 22,
            Padding = new Thickness(0),
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            ToolTip = "Copy code",
            HorizontalAlignment = HorizontalAlignment.Right,
            Opacity = 0.6,
        };
        copyBtn.SetResourceReference(Button.BackgroundProperty, "CA_CodeBlockBackgroundBrush");
        copyBtn.SetResourceReference(Button.ForegroundProperty, "CA_MessageForegroundBrush");
        copyBtn.Click += async (sender, _) =>
        {
            Clipboard.SetText(trimmedCode);
            if (sender is Button btn)
            {
                btn.Content = "\uE73E"; // checkmark
                btn.ToolTip = "Copied!";
                await Task.Delay(2000);
                btn.Content = "\uE8C8";
                btn.ToolTip = "Copy code";
            }
        };
        DockPanel.SetDock(copyBtn, Dock.Right);
        header.Children.Insert(0, copyBtn);

        Grid.SetRow(header, 0);
        grid.Children.Add(header);

        // ── Code content: GlyphRun/DrawingContext renderer (whfmt-driven) ─
        var canvas = new ChatCodeBlockCanvas
        {
            Code = trimmedCode,
            Language = language,
            Margin = new Thickness(0, 0, 0, 8),
        };

        Grid.SetRow(canvas, 1);
        grid.Children.Add(canvas);

        outerBorder.Child = grid;
        return outerBorder;
    }
}
