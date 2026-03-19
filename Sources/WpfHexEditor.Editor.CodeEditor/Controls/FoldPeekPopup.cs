// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: FoldPeekPopup.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-18
// Description:
//     VS-style fold peek popup — shown after hovering over a collapsed
//     fold label for ~600 ms. Displays the hidden code lines in a
//     scrollable, syntax-coloured code preview window.
//
// Architecture Notes:
//     Derives from Popup (StaysOpen=true, AllowsTransparency=true).
//     Content is a TextBlock (TextWrapping.NoWrap) with colored Run + LineBreak
//     inlines so that the popup auto-sizes horizontally to the widest code line.
//     A RichTextBox/FlowDocument was tried but measured poorly (fixed PageWidth
//     prevented auto-sizing; ScrollViewer could not derive content width).
//     Theme tokens reused from CE_QuickInfo_* — no new token additions.
// ==========================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using WpfHexEditor.Editor.CodeEditor.Folding;
using WpfHexEditor.Editor.CodeEditor.Helpers;
using WpfHexEditor.Editor.CodeEditor.Models;

namespace WpfHexEditor.Editor.CodeEditor.Controls;

/// <summary>
/// Peek popup that previews the hidden content of a collapsed fold region
/// with VS-like syntax colouring.
/// Shown after a ~600 ms hover over a fold label; dismissed on mouse-leave.
/// </summary>
internal sealed class FoldPeekPopup : Popup
{
    private const int    MaxPreviewLines = 33;
    private const double MaxPopupHeight  = 500;
    private const double MaxPopupWidth   = 900;

    private readonly Border       _border;
    private readonly ScrollViewer _scrollViewer;
    private readonly TextBlock    _textBlock;

    public FoldPeekPopup()
    {
        StaysOpen          = true;
        AllowsTransparency = true;
        Placement          = PlacementMode.Relative;

        // TextBlock — NoWrap so the popup expands horizontally to the widest line.
        // Colored Run + LineBreak inlines are added per Show() call.
        _textBlock = new TextBlock
        {
            TextWrapping = TextWrapping.NoWrap,
            Padding      = new Thickness(10, 8, 10, 8),
        };
        _textBlock.SetResourceReference(TextBlock.ForegroundProperty, "CE_QuickInfo_Text");

        // ScrollViewer — clips content to MaxPopupWidth/Height.
        // Horizontal scroll is disabled; long lines fade out via OpacityMask instead.
        // Vertical scroll is hidden (mouse-wheel works but no bar chrome shown).
        _scrollViewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility   = ScrollBarVisibility.Hidden,
            MaxHeight = MaxPopupHeight,
            MaxWidth  = MaxPopupWidth,
            Content   = _textBlock,
        };
        // Recompute the 20 px right-edge fade whenever the popup is sized/resized.
        _scrollViewer.SizeChanged += (_, _) => UpdateOpacityMask();

        // Border — themed background + rounded corners + drop shadow.
        _border = new Border
        {
            CornerRadius    = new CornerRadius(4),
            BorderThickness = new Thickness(1),
            Child           = _scrollViewer,
            Effect          = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius  = 8,
                ShadowDepth = 2,
                Opacity     = 0.4,
                Color       = Colors.Black,
            },
        };
        _border.SetResourceReference(Border.BackgroundProperty,  "CE_QuickInfo_Background");
        _border.SetResourceReference(Border.BorderBrushProperty, "CE_QuickInfo_Border");

        Child = _border;
    }

    /// <summary>
    /// Populates and opens the popup anchored below <paramref name="labelRect"/>.
    /// </summary>
    internal void Show(
        FrameworkElement         host,
        FoldingRegion            region,
        IReadOnlyList<CodeLine>  allLines,
        Typeface                 typeface,
        double                   fontSize,
        Rect                     labelRect,
        ISyntaxHighlighter?      highlighter = null)
    {
        // Apply font from the editor so code looks consistent.
        _textBlock.FontFamily = typeface.FontFamily;
        _textBlock.FontSize   = fontSize;

        // Collect the lines to preview (StartLine through EndLine inclusive).
        int startLine = region.StartLine;
        int endLine   = Math.Min(region.EndLine, allLines.Count - 1);

        var rawLines = new List<string>(endLine - startLine + 1);
        for (int i = startLine; i <= endLine; i++)
            rawLines.Add(allLines[i].Text ?? string.Empty);

        // Strip common leading whitespace so preview is left-aligned.
        var nonEmpty     = rawLines.Where(l => l.Trim().Length > 0).ToList();
        int commonIndent = nonEmpty.Count > 0
            ? nonEmpty.Min(l => l.Length - l.TrimStart().Length)
            : 0;

        var strippedLines = rawLines
            .Select(l => l.Length >= commonIndent ? l[commonIndent..] : l)
            .ToList();

        // Cap at MaxPreviewLines — show plain "…" if truncated.
        bool truncated = strippedLines.Count > MaxPreviewLines;
        if (truncated)
        {
            strippedLines = strippedLines.Take(MaxPreviewLines).ToList();
            strippedLines.Add("\u2026");
        }

        // Reset highlighter state so multi-line context starts fresh for this region.
        highlighter?.Reset();

        // Build TextBlock inlines: colored Runs per line + LineBreak between lines.
        _textBlock.Inlines.Clear();

        int absoluteLine = startLine;
        for (int li = 0; li < strippedLines.Count; li++, absoluteLine++)
        {
            string lineText = strippedLines[li];

            // Add LineBreak before every line after the first.
            if (li > 0)
                _textBlock.Inlines.Add(new LineBreak());

            bool isEllipsis = truncated && li == strippedLines.Count - 1;

            if (highlighter != null && !isEllipsis && li < rawLines.Count)
            {
                // Highlight the ORIGINAL line (with indent) so column offsets are correct.
                string originalLine = rawLines[li];
                var    tokens       = highlighter.Highlight(originalLine, absoluteLine);

                if (tokens.Count == 0)
                {
                    _textBlock.Inlines.Add(new Run(lineText));
                }
                else
                {
                    // Gap-fill: emit a plain Run for any text the highlighter skipped
                    // (spaces, operators, punctuation), then a colored Run for each token.
                    // ISyntaxHighlighter implementations return only matched-pattern tokens —
                    // the gaps between them must be filled explicitly using originalLine.
                    int pos = 0;
                    foreach (var token in tokens)
                    {
                        // Emit gap text before this token (clamped past commonIndent).
                        int gapStart = Math.Max(pos, commonIndent);
                        int gapEnd   = Math.Max(token.StartColumn, commonIndent);
                        if (gapEnd > gapStart && gapEnd <= originalLine.Length)
                            _textBlock.Inlines.Add(new Run(originalLine[gapStart..gapEnd]));

                        // Emit colored token text, skipping any prefix inside the stripped indent.
                        int    skip    = Math.Max(0, commonIndent - token.StartColumn);
                        string runText = skip < token.Text.Length ? token.Text[skip..] : string.Empty;
                        if (runText.Length > 0)
                        {
                            var run = new Run(runText) { Foreground = token.Foreground };
                            if (token.IsBold)   run.FontWeight = FontWeights.Bold;
                            if (token.IsItalic) run.FontStyle  = FontStyles.Italic;
                            _textBlock.Inlines.Add(run);
                        }
                        pos = token.StartColumn + token.Text.Length;
                    }

                    // Tail: emit any text after the last token.
                    int tailStart = Math.Max(pos, commonIndent);
                    if (tailStart < originalLine.Length)
                        _textBlock.Inlines.Add(new Run(originalLine[tailStart..]));

                    // Safety fallback: if nothing was emitted (all tokens inside stripped indent).
                    if (_textBlock.Inlines.Count == 0 || _textBlock.Inlines.Last() is LineBreak)
                        _textBlock.Inlines.Add(new Run(lineText));
                }
            }
            else
            {
                // No highlighter, or truncation ellipsis line — plain text.
                _textBlock.Inlines.Add(new Run(lineText));
            }
        }

        // Position below-left of the label rect.
        PlacementTarget  = host;
        HorizontalOffset = labelRect.Left;
        VerticalOffset   = labelRect.Bottom + 4;

        _scrollViewer.ScrollToTop();
        _scrollViewer.ScrollToLeftEnd();

        IsOpen = true;
    }

    /// <summary>
    /// Applies a fixed 20 px right-edge fade to <see cref="_scrollViewer"/> so that
    /// lines wider than the popup fade to transparent instead of hard-clipping.
    /// Theme-agnostic: uses <c>OpacityMask</c> so the Border's background shows through.
    /// </summary>
    private void UpdateOpacityMask()
    {
        double w = _scrollViewer.ActualWidth;
        if (w <= 20)
        {
            _scrollViewer.OpacityMask = null;
            return;
        }

        // fadeStop is the relative position (0–1) at which the 20 px fade begins.
        double fadeStop = (w - 20.0) / w;

        var mask = new LinearGradientBrush
        {
            StartPoint  = new Point(0, 0),
            EndPoint    = new Point(1, 0),
            MappingMode = BrushMappingMode.RelativeToBoundingBox,
        };
        mask.GradientStops.Add(new GradientStop(Colors.Black,       fadeStop));
        mask.GradientStops.Add(new GradientStop(Colors.Transparent, 1.0));
        _scrollViewer.OpacityMask = mask;
    }

    /// <summary>Hides the popup immediately.</summary>
    internal void Hide()
    {
        IsOpen = false;
    }
}
