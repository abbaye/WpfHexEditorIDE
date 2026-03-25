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
//     Theme resources CE_StickyScroll* are resolved via Application.Current.
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
    // ── State ──────────────────────────────────────────────────────────────

    private IReadOnlyList<StickyScrollEntry> _entries     = Array.Empty<StickyScrollEntry>();
    private double                           _lineHeight;
    private double                           _charWidth;
    private Typeface?                        _typeface;
    private double                           _fontSize;
    private double                           _textX;         // left edge of text area (after gutter)
    private double                           _pixelsPerDip  = 1.0;
    private bool                             _syntaxHighlight = true;
    private bool                             _clickToNavigate = true;

    // ── Events ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Raised when the user clicks a row.  Argument is the 0-based StartLine of the scope.
    /// </summary>
    public event EventHandler<int>? ScopeClicked;

    // ── Public API ─────────────────────────────────────────────────────────

    /// <summary>
    /// Update the scope chain entries and trigger a redraw.
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
        bool clickToNavigate)
    {
        _entries          = entries;
        _lineHeight       = lineHeight;
        _charWidth        = charWidth;
        _typeface         = typeface;
        _fontSize         = fontSize;
        _textX            = textX;
        _pixelsPerDip     = pixelsPerDip;
        _syntaxHighlight  = syntaxHighlight;
        _clickToNavigate  = clickToNavigate;

        InvalidateVisual();
    }

    /// <summary>Returns the height this control needs for its current entry count.</summary>
    public double RequiredHeight => _entries.Count * _lineHeight;

    // ── Rendering ──────────────────────────────────────────────────────────

    protected override void OnRender(DrawingContext dc)
    {
        if (_entries.Count == 0 || _lineHeight <= 0 || _typeface is null) return;

        var bg     = TryFindRes("CE_StickyScrollBackground") as Brush
                     ?? new SolidColorBrush(Color.FromArgb(0xF0, 0x20, 0x20, 0x20));
        var border = TryFindRes("CE_StickyScrollBorder") as Brush
                     ?? new SolidColorBrush(Color.FromArgb(0x60, 0x80, 0x80, 0x80));
        var fg     = TryFindRes("CE_StickyScrollForeground") as Brush
                     ?? Brushes.LightGray;

        double w = ActualWidth;
        double h = _entries.Count * _lineHeight;

        // Background panel
        dc.DrawRectangle(bg, null, new Rect(0, 0, w, h));

        // Bottom border line
        var borderPen = new Pen(border, 1.0);
        dc.DrawLine(borderPen, new Point(0, h), new Point(w, h));

        // Render each scope line
        for (int i = 0; i < _entries.Count; i++)
        {
            var entry = _entries[i];
            double y = i * _lineHeight;

            if (_syntaxHighlight && entry.Tokens.Count > 0)
            {
                // Render token by token
                foreach (var token in entry.Tokens)
                {
                    double tokenX = _textX + token.StartColumn * _charWidth;
                    if (tokenX > w) break;

                    var typeface = token.IsBold ? new Typeface(_typeface.FontFamily,
                        FontStyles.Normal, FontWeights.Bold, FontStretches.Normal) : _typeface;
                    var brush = token.Foreground ?? fg;

                    var ft = new FormattedText(
                        token.Text, CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight, typeface, _fontSize, brush, _pixelsPerDip);

                    if (token.IsItalic) ft.SetFontStyle(FontStyles.Italic);
                    dc.DrawText(ft, new Point(tokenX, y));
                }
            }
            else
            {
                // Plain text fallback
                var ft = new FormattedText(
                    entry.PlainText, CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight, _typeface, _fontSize, fg, _pixelsPerDip);
                dc.DrawText(ft, new Point(_textX, y));
            }

            // Subtle separator between rows (except last)
            if (i < _entries.Count - 1)
            {
                var sepPen = new Pen(new SolidColorBrush(Color.FromArgb(0x20, 0x80, 0x80, 0x80)), 1.0);
                dc.DrawLine(sepPen, new Point(0, y + _lineHeight), new Point(w, y + _lineHeight));
            }
        }
    }

    // ── Mouse interaction ──────────────────────────────────────────────────

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (!_clickToNavigate || _lineHeight <= 0 || _entries.Count == 0) return;

        var pos   = e.GetPosition(this);
        int rowIdx = Math.Max(0, Math.Min((int)(pos.Y / _lineHeight), _entries.Count - 1));
        ScopeClicked?.Invoke(this, _entries[rowIdx].StartLine);
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        Cursor = _clickToNavigate && _entries.Count > 0 ? Cursors.Hand : null;
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static object? TryFindRes(string key)
    {
        try { return Application.Current?.TryFindResource(key); }
        catch { return null; }
    }
}
