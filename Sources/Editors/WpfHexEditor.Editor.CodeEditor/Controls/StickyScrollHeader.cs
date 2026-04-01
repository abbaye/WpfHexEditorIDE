// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Controls/StickyScrollHeader.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-25
// Description:
//     Sticky-scroll header bar that pins the innermost N scope signature
//     lines at the top of the code editor while the user scrolls.
//     Each row shows the text of the scope-opening line; clicking a row
//     fires ScopeClicked to scroll the editor to that scope's start.
//
// Architecture Notes:
//     Pattern: Custom FrameworkElement with DrawingContext rendering.
//     Hosted as a visual child of CodeEditor (not a XAML element).
//     Receives pre-computed scope chain from CodeEditor.Rendering.cs.
//     Theme resources CE_StickyScroll* are resolved once in Update()
//     and cached as fields — TryFindResource is never called in OnRender.
//     FormattedText objects are also cached per entry in Update() so
//     OnRender only calls DrawText on pre-built objects (zero allocation).
// ==========================================================

using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.Editor.CodeEditor.Helpers;

namespace WpfHexEditor.Editor.CodeEditor.Controls;

/// <summary>
/// Represents one line shown in the sticky-scroll header.
/// </summary>
internal readonly record struct StickyScrollEntry(int StartLine, IReadOnlyList<SyntaxHighlightToken> Tokens, string PlainText);

/// <summary>
/// Sticky-scroll header element: renders N scope signature lines pinned at
/// the top of the CodeEditor viewport.  Updated by <c>UpdateStickyScrollHeader()</c>
/// in <c>CodeEditor.Rendering.cs</c>.
/// </summary>
internal sealed class StickyScrollHeader : FrameworkElement
{
    // ── Static frozen fallback brushes/pens (allocated once per AppDomain) ──

    private static readonly Brush s_defaultBg;
    private static readonly Brush s_defaultBorder;
    private static readonly Brush s_defaultFg     = Brushes.LightGray;
    private static readonly Brush s_sepBrush;
    private static readonly Brush s_hoverBrush;

    static StickyScrollHeader()
    {
        s_defaultBg = new SolidColorBrush(Color.FromArgb(0xF0, 0x20, 0x20, 0x20));
        s_defaultBg.Freeze();
        s_defaultBorder = new SolidColorBrush(Color.FromArgb(0x60, 0x80, 0x80, 0x80));
        s_defaultBorder.Freeze();
        s_sepBrush = new SolidColorBrush(Color.FromArgb(0x20, 0x80, 0x80, 0x80));
        s_sepBrush.Freeze();
        s_hoverBrush = new SolidColorBrush(Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF));
        s_hoverBrush.Freeze();
    }

    // ── Render state (set once in Update, used many times in OnRender) ──────

    private IReadOnlyList<StickyScrollEntry> _entries     = Array.Empty<StickyScrollEntry>();
    private double                           _lineHeight;
    private bool                             _clickToNavigate = true;

    // Cached brushes/pens (resolved from theme in Update(), never in OnRender).
    private Brush?  _cachedBg;
    private Brush?  _cachedFg;
    private Brush?  _cachedHoverBrush;
    private Pen?    _cachedBorderPen;
    private Pen?    _cachedSepPen;

    // Hover state — tracked in OnMouseMove/OnMouseLeave.
    private int     _hoverRowIndex = -1;

    // Cached FormattedText rows (built in Update(), drawn in OnRender).
    // Index = entry index; each item is a list of (x-offset, FormattedText) pairs.
    private List<List<(double X, FormattedText Ft)>>? _cachedRows;

    // ── Events ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Raised when the user clicks a row.  Argument is the 0-based StartLine of the scope.
    /// </summary>
    public event EventHandler<int>? ScopeClicked;

    // ── Public API ─────────────────────────────────────────────────────────

    /// <summary>
    /// Update the scope chain entries and trigger a redraw.
    /// Called only when <c>_firstVisibleLine</c> changes — not on every render frame.
    /// </summary>
    public void Update(
        IReadOnlyList<StickyScrollEntry> entries,
        double lineHeight,
        double charWidth,
        Typeface typeface,
        double fontSize,
        double textX,
        double pixelsPerDip,
        bool syntaxHighlight,
        bool clickToNavigate,
        bool showLineNumbers,
        double lineNumberWidth,
        double lineNumberMargin,
        Typeface? lineNumberTypeface,
        Brush? lineNumberForeground)
    {
        _entries         = entries;
        _lineHeight      = lineHeight;
        _clickToNavigate = clickToNavigate;

        // Resolve theme brushes once here; OnRender uses cached fields.
        _cachedBg         = TryFindRes("CE_StickyScrollBackground") as Brush ?? s_defaultBg;
        _cachedFg         = TryFindRes("CE_StickyScrollForeground") as Brush ?? s_defaultFg;
        _cachedHoverBrush = TryFindRes("CE_StickyScrollHover")      as Brush ?? s_hoverBrush;
        var border        = TryFindRes("CE_StickyScrollBorder")      as Brush ?? s_defaultBorder;

        var borderPen = new Pen(border, 1.0);
        borderPen.Freeze();
        _cachedBorderPen = borderPen;

        var sepPen = new Pen(s_sepBrush, 1.0);
        sepPen.Freeze();
        _cachedSepPen = sepPen;

        // Pre-build FormattedText objects so OnRender only calls DrawText.
        _cachedRows = new List<List<(double, FormattedText)>>(entries.Count);

        var lnBrush    = lineNumberForeground ?? s_defaultFg;
        var lnTypeface = lineNumberTypeface   ?? typeface;

        foreach (var entry in entries)
        {
            var rowSegments = new List<(double X, FormattedText Ft)>();

            // Line number — right-aligned in the gutter, same style as editor line numbers.
            if (showLineNumbers && lineNumberWidth > 0)
            {
                var lnFt = new FormattedText(
                    (entry.StartLine + 1).ToString(),
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    lnTypeface, fontSize, lnBrush, pixelsPerDip);
                double lnX = lineNumberWidth - lnFt.Width - lineNumberMargin;
                rowSegments.Add((lnX, lnFt));
            }

            if (syntaxHighlight && entry.Tokens.Count > 0)
            {
                // Base pass: draw full line in default foreground so plain text
                // (identifiers, spaces, punctuation) not covered by any token stays visible.
                var baseFt = new FormattedText(
                    entry.PlainText, CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight, typeface, fontSize, _cachedFg, pixelsPerDip);
                rowSegments.Add((textX, baseFt));

                foreach (var token in entry.Tokens)
                {
                    double tokenX = textX + StickyVisualX(entry.PlainText, token.StartColumn, charWidth);
                    var tf = token.IsBold
                        ? new Typeface(typeface.FontFamily, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal)
                        : typeface;
                    var brush = token.Foreground ?? _cachedFg;
                    var ft = new FormattedText(
                        token.Text, CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight, tf, fontSize, brush, pixelsPerDip);
                    if (token.IsItalic) ft.SetFontStyle(FontStyles.Italic);
                    rowSegments.Add((tokenX, ft));
                }
            }
            else
            {
                var ft = new FormattedText(
                    entry.PlainText, CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight, typeface, fontSize, _cachedFg, pixelsPerDip);
                rowSegments.Add((textX, ft));
            }

            _cachedRows.Add(rowSegments);
        }

        InvalidateVisual();
    }

    /// <summary>Returns the height this control needs for its current entry count.</summary>
    public double RequiredHeight => _entries.Count * _lineHeight;

    // ── Rendering ──────────────────────────────────────────────────────────

    protected override void OnRender(DrawingContext dc)
    {
        if (_entries.Count == 0 || _lineHeight <= 0
            || _cachedBg is null || _cachedRows is null) return;

        double w = ActualWidth;
        double h = _entries.Count * _lineHeight;

        // Background panel — single rect, no allocation.
        dc.DrawRectangle(_cachedBg, null, new Rect(0, 0, w, h));

        // Hover highlight — drawn after background, before text.
        if (_hoverRowIndex >= 0 && _hoverRowIndex < _entries.Count && _cachedHoverBrush is not null)
            dc.DrawRectangle(_cachedHoverBrush, null,
                new Rect(0, _hoverRowIndex * _lineHeight, w, _lineHeight));

        // Bottom border line.
        dc.DrawLine(_cachedBorderPen, new Point(0, h), new Point(w, h));

        // Render each scope line from pre-built FormattedText objects.
        for (int i = 0; i < _cachedRows.Count; i++)
        {
            double y = i * _lineHeight;

            foreach (var (x, ft) in _cachedRows[i])
                dc.DrawText(ft, new Point(x, y));

            // Subtle separator between rows (except last).
            if (i < _entries.Count - 1)
                dc.DrawLine(_cachedSepPen, new Point(0, y + _lineHeight), new Point(w, y + _lineHeight));
        }
    }

    // ── Mouse interaction ──────────────────────────────────────────────────

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (!_clickToNavigate || _lineHeight <= 0 || _entries.Count == 0) return;

        var pos    = e.GetPosition(this);
        int rowIdx = Math.Max(0, Math.Min((int)(pos.Y / _lineHeight), _entries.Count - 1));
        ScopeClicked?.Invoke(this, _entries[rowIdx].StartLine);
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        Cursor = _clickToNavigate && _entries.Count > 0 ? Cursors.Hand : null;

        if (_lineHeight <= 0 || _entries.Count == 0) return;
        int idx = Math.Max(0, Math.Min((int)(e.GetPosition(this).Y / _lineHeight), _entries.Count - 1));
        if (idx == _hoverRowIndex) return;
        _hoverRowIndex = idx;
        InvalidateVisual();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hoverRowIndex == -1) return;
        _hoverRowIndex = -1;
        InvalidateVisual();
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    /// <summary>
    /// Computes the visual X offset for <paramref name="col"/> in <paramref name="lineText"/>,
    /// expanding tabs to <c>4 × charWidth</c> — mirrors GlyphRunRenderer.ComputeVisualX.
    /// </summary>
    private static double StickyVisualX(string lineText, int col, double charWidth)
    {
        const int tabSize = 4;
        double x = 0;
        int limit = Math.Min(col, lineText.Length);
        for (int i = 0; i < limit; i++)
            x += lineText[i] == '\t' ? charWidth * tabSize : charWidth;
        return x;
    }

    private static object? TryFindRes(string key)
    {
        try { return Application.Current?.TryFindResource(key); }
        catch { return null; }
    }
}
