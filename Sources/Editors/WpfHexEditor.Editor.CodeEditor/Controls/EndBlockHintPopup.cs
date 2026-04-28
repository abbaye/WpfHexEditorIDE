// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Controls/EndBlockHintPopup.cs
// Description:
//     Compact VS-style popup shown after hovering over an end-of-block
//     token (}, #endregion, </Tag>, end, …). Displays the matching
//     opening line(s) with syntax coloring, line number, line count,
//     and a "Go to →" navigation link.
// Architecture:
//     Derives from Popup (StaysOpen=true, AllowsTransparency=true).
//     Detection is FoldingRegion-based (region.EndLine == hoveredLine) —
//     no text parsing of }, #endregion etc.
//     Grace-timer pattern identical to QuickInfoPopup (200 ms).
//     All colours via SetResourceReference (ET_* tokens — no hardcoded values).
//     Navigate-to-opener via NavigationRequested event (CodeEditor handles it).
// ==========================================================

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using WpfHexEditor.Editor.CodeEditor.Folding;
using WpfHexEditor.Editor.CodeEditor.Helpers;
using WpfHexEditor.Editor.CodeEditor.Models;

namespace WpfHexEditor.Editor.CodeEditor.Controls;

/// <summary>
/// Compact popup shown when the user hovers over a closing block token.
/// Shows the matching opening line(s) with syntax-coloured text.
/// </summary>
internal sealed class EndBlockHintPopup : Popup
{
    // ── Grace-timer ───────────────────────────────────────────────────────────

    private readonly DispatcherTimer _graceTimer;
    private bool _mouseInsidePopup;

    // ── Visual tree ───────────────────────────────────────────────────────────

    private readonly StackPanel _headerLines;  // one TextBlock per context line
    private readonly StackPanel _metaRow;      // pills + name + navigation link

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Fires with 0-based StartLine when the user clicks "Go to →".</summary>
    internal event Action<int>? NavigationRequested;

    // ── Cached open region ────────────────────────────────────────────────────

    private int _currentStartLine;

    // ── Constructor ───────────────────────────────────────────────────────────

    internal EndBlockHintPopup()
    {
        StaysOpen          = true;
        AllowsTransparency = true;
        Placement          = PlacementMode.Relative;

        _graceTimer          = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _graceTimer.Tick    += (_, _) => { _graceTimer.Stop(); IsOpen = false; };

        // ── Header strip (syntax-coloured opening lines) ──────────────────────
        _headerLines = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Margin      = new Thickness(10, 7, 10, 6),
        };

        var headerBorder = new Border { Padding = new Thickness(0) };
        headerBorder.SetResourceReference(Border.BackgroundProperty, "ET_HeaderBackground");
        headerBorder.Child = _headerLines;

        // ── Separator ─────────────────────────────────────────────────────────
        var sep = new Border { Height = 1, Margin = new Thickness(0) };
        sep.SetResourceReference(Border.BackgroundProperty, "ET_PopupBorderBrush");

        // ── Meta row (pills + name + nav link) ────────────────────────────────
        _metaRow = new StackPanel
        {
            Orientation       = Orientation.Horizontal,
            Margin            = new Thickness(8, 5, 8, 5),
            VerticalAlignment = VerticalAlignment.Center,
        };

        var metaBorder = new Border();
        metaBorder.SetResourceReference(Border.BackgroundProperty, "ET_PopupBackground");
        metaBorder.Child = _metaRow;

        // ── Outer stack ───────────────────────────────────────────────────────
        var stack = new StackPanel { Orientation = Orientation.Vertical };
        stack.Children.Add(headerBorder);
        stack.Children.Add(sep);
        stack.Children.Add(metaBorder);

        // ── Outer border ─────────────────────────────────────────────────────
        var outerBorder = new Border
        {
            CornerRadius    = new CornerRadius(4),
            BorderThickness = new Thickness(1),
            MinWidth        = 200,
            MaxWidth        = 700,
            Child           = stack,
            Effect = new DropShadowEffect
            {
                BlurRadius  = 8,
                ShadowDepth = 2,
                Opacity     = 0.35,
                Color       = Colors.Black,
            },
        };
        outerBorder.SetResourceReference(Border.BackgroundProperty,  "ET_PopupBackground");
        outerBorder.SetResourceReference(Border.BorderBrushProperty, "ET_PopupBorderBrush");

        Child = outerBorder;

        // Mouse tracking — keep popup open when cursor enters it
        outerBorder.MouseEnter += (_, _) => { _mouseInsidePopup = true;  _graceTimer.Stop(); };
        outerBorder.MouseLeave += (_, _) => { _mouseInsidePopup = false; _graceTimer.Start(); };

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { IsOpen = false; e.Handled = true; }
        };

        if (Application.Current is not null)
            Application.Current.Deactivated += OnApplicationDeactivated;
    }

    private void OnApplicationDeactivated(object? sender, EventArgs e)
        => Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => IsOpen = false));

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Populates and opens the popup positioned above the closing token.
    /// </summary>
    internal void Show(
        FrameworkElement        host,
        FoldingRegion           region,
        IReadOnlyList<CodeLine> allLines,
        Typeface                typeface,
        double                  fontSize,
        Rect                    closingTokenRect,   // element-local coords of the end-token line
        ISyntaxHighlighter?     highlighter,
        int                     maxContextLines = 3,
        bool                    showLineNumber  = true,
        bool                    showLineCount   = true)
    {
        _currentStartLine = region.StartLine;
        _graceTimer.Stop();
        _mouseInsidePopup = false;

        PopulateHeader(region, allLines, typeface, fontSize, highlighter, maxContextLines);
        PopulateMeta(region, showLineNumber, showLineCount);

        PlacementTarget  = host;

        // Position: left-align with text area, above the closing-token line.
        // We open below during the current frame then override; WPF will clamp to screen.
        HorizontalOffset = closingTokenRect.Left;
        VerticalOffset   = closingTokenRect.Top - 4;  // WPF will push up if needed

        IsOpen = true;
    }

    /// <summary>Immediately closes the popup and cancels the grace timer.</summary>
    internal void Hide()
    {
        _graceTimer.Stop();
        IsOpen = false;
    }

    /// <summary>
    /// Called by CodeEditor.OnMouseLeave. Starts the 200 ms grace timer so the user
    /// can move the mouse into the popup before it auto-dismisses.
    /// </summary>
    internal void OnEditorMouseLeft()
    {
        if (!IsOpen || _mouseInsidePopup) return;
        _graceTimer.Stop();
        _graceTimer.Start();
    }

    internal void Dispose()
    {
        if (Application.Current is not null)
            Application.Current.Deactivated -= OnApplicationDeactivated;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void PopulateHeader(
        FoldingRegion           region,
        IReadOnlyList<CodeLine> allLines,
        Typeface                typeface,
        double                  fontSize,
        ISyntaxHighlighter?     highlighter,
        int                     maxCtx)
    {
        _headerLines.Children.Clear();

        int startLine = region.StartLine;
        int endBefore = region.EndLine - 1; // don't include the closing line itself
        int lastCtx   = Math.Min(startLine + maxCtx - 1, endBefore);

        if (startLine >= allLines.Count) return;

        // Collect raw lines in range
        var rawLines = new List<string>();
        for (int i = startLine; i <= lastCtx && i < allLines.Count; i++)
            rawLines.Add(allLines[i].Text ?? string.Empty);

        // Strip common leading whitespace for clean display
        int commonIndent = 0;
        var nonEmpty = new List<string>();
        foreach (var l in rawLines) if (l.Trim().Length > 0) nonEmpty.Add(l);
        if (nonEmpty.Count > 0)
            foreach (var l in nonEmpty) { int sp = l.Length - l.TrimStart().Length; if (commonIndent == 0 || sp < commonIndent) commonIndent = sp; }

        // Reset highlighter context so multi-line state starts fresh
        highlighter?.Reset();

        for (int li = 0; li < rawLines.Count; li++)
        {
            string rawLine = rawLines[li];
            string stripped = rawLine.Length >= commonIndent ? rawLine[commonIndent..] : rawLine;

            var tb = new TextBlock
            {
                TextWrapping  = TextWrapping.NoWrap,
                FontFamily    = typeface.FontFamily,
                FontSize      = fontSize * 0.95,
            };
            tb.SetResourceReference(TextBlock.ForegroundProperty, "ET_HeaderForeground");

            int absLine = startLine + li;

            if (highlighter != null && rawLine.Length > 0)
            {
                var tokens = highlighter.Highlight(rawLine, absLine);
                if (tokens.Count == 0)
                {
                    tb.Text = stripped;
                }
                else
                {
                    int pos = 0;
                    foreach (var token in tokens)
                    {
                        int gapStart = Math.Max(pos, commonIndent);
                        int gapEnd   = Math.Max(token.StartColumn, commonIndent);
                        if (gapEnd > gapStart && gapEnd <= rawLine.Length)
                            tb.Inlines.Add(new Run(rawLine[gapStart..gapEnd]));

                        int    skip    = Math.Max(0, commonIndent - token.StartColumn);
                        string runText = skip < token.Text.Length ? token.Text[skip..] : string.Empty;
                        if (runText.Length > 0)
                        {
                            var run = new Run(runText) { Foreground = token.Foreground };
                            if (token.IsBold)   run.FontWeight = FontWeights.Bold;
                            if (token.IsItalic) run.FontStyle  = FontStyles.Italic;
                            tb.Inlines.Add(run);
                        }
                        pos = token.StartColumn + token.Text.Length;
                    }
                    // trailing text after last token
                    if (pos < rawLine.Length)
                    {
                        int s = Math.Max(pos, commonIndent);
                        if (s < rawLine.Length) tb.Inlines.Add(new Run(rawLine[s..]));
                    }
                    // If no inlines were added (all skipped), fall back to stripped text
                    if (tb.Inlines.Count == 0) tb.Text = stripped;
                }
            }
            else
            {
                tb.Text = stripped;
            }

            _headerLines.Children.Add(tb);
        }

        // Ellipsis if more lines were cut off
        bool truncated = (lastCtx < endBefore) && (endBefore >= startLine + 1);
        if (truncated)
        {
            var dots = new TextBlock
            {
                Text       = "\u2026",
                FontFamily = typeface.FontFamily,
                FontSize   = fontSize * 0.85,
                Opacity    = 0.55,
            };
            _headerLines.Children.Add(dots);
        }
    }

    private void PopulateMeta(FoldingRegion region, bool showLineNumber = true, bool showLineCount = true)
    {
        _metaRow.Children.Clear();

        int lineCount = region.HiddenLineCount + 2; // start + hidden + end

        // "Line N" pill — controlled by whfmt endOfBlockHint.showLineNumber
        if (showLineNumber)
            _metaRow.Children.Add(MakePill($"Line {region.StartLine + 1}", "ET_LineNumberPillBg"));

        // "N lines" pill — controlled by whfmt endOfBlockHint.showLineCount
        if (showLineCount)
            _metaRow.Children.Add(MakePill($"{lineCount} lines", "ET_LineCountPillBg"));

        // Region name (if named, e.g. #region Value Converters)
        if (!string.IsNullOrWhiteSpace(region.Name))
        {
            var nameText = new TextBlock
            {
                Text              = $"· {region.Name}",
                FontSize          = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Margin            = new Thickness(5, 0, 0, 0),
            };
            nameText.SetResourceReference(TextBlock.ForegroundProperty, "ET_MetaForeground");
            _metaRow.Children.Add(nameText);
        }

        // Spacer
        _metaRow.Children.Add(new Border { Width = 6 });

        // "Go to →" navigation link (right-aligned via DockPanel trick — use StackPanel filler)
        var filler = new Border { HorizontalAlignment = HorizontalAlignment.Stretch };
        _metaRow.HorizontalAlignment = HorizontalAlignment.Stretch;

        var navLink = new TextBlock
        {
            Text              = "Go to \u2192",
            FontSize          = 11,
            Cursor            = Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(0, 0, 2, 0),
        };
        navLink.SetResourceReference(TextBlock.ForegroundProperty, "ET_AccentBrush");
        navLink.MouseLeftButtonUp += (_, _) =>
        {
            IsOpen = false;
            NavigationRequested?.Invoke(_currentStartLine);
        };
        _metaRow.Children.Add(navLink);
    }

    private static Border MakePill(string text, string bgTokenKey)
    {
        var tb = new TextBlock
        {
            Text              = text,
            FontSize          = 10,
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(0),
        };
        tb.SetResourceReference(TextBlock.ForegroundProperty, "ET_HeaderForeground");

        var pill = new Border
        {
            CornerRadius = new CornerRadius(3),
            Padding      = new Thickness(5, 1, 5, 1),
            Margin       = new Thickness(0, 0, 4, 0),
            Child        = tb,
        };
        pill.SetResourceReference(Border.BackgroundProperty, bgTokenKey);
        return pill;
    }
}
