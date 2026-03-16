// ==========================================================
// Project: WpfHexEditor.Editor.TextEditor
// File: Services/TextLinkAdorner.cs
// Author: Derek Tremblay
// Created: 2026-03-16
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     WPF Adorner that renders underline decorations for TextLink ranges
//     in the TextViewport and handles Ctrl+Click goto-definition navigation.
//     Uses the viewport's public layout metrics (LineHeight, CharWidth,
//     LineNumberColumnWidth, FirstVisibleLine, HorizontalOffset) to position
//     underlines accurately.
//
// Architecture Notes:
//     Pattern: Decorator (WPF Adorner layer).
//     Theme: underline color resolved via Application.Current.TryFindResource
//     with "#569CD6" fallback (PFP_AccentBrush token).
//     Hover: Cursors.Hand is applied to the adorner; cursor is restored on leave.
// ==========================================================

using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.Editor.TextEditor.Controls;
using WpfHexEditor.Editor.TextEditor.Models;
using WpfHexEditor.Editor.TextEditor.ViewModels;

namespace WpfHexEditor.Editor.TextEditor.Services;

/// <summary>
/// Renders Ctrl+Click-navigable underlines over <see cref="TextViewport"/> for
/// each registered <see cref="TextLink"/>.
/// </summary>
internal sealed class TextLinkAdorner : Adorner
{
    private static readonly Brush FallbackUnderlineBrush;

    static TextLinkAdorner()
    {
        FallbackUnderlineBrush = new SolidColorBrush(Color.FromRgb(0x56, 0x9C, 0xD6));
        FallbackUnderlineBrush.Freeze();
    }

    // ── Fields ────────────────────────────────────────────────────────────────

    private readonly TextViewport        _viewport;
    private readonly TextEditorViewModel _vm;
    private IReadOnlyList<TextLink>      _links = [];
    private TextLink?                    _hoveredLink;
    private Brush?                       _underlineBrush;

    // ── Constructor ───────────────────────────────────────────────────────────

    public TextLinkAdorner(TextViewport viewport, TextEditorViewModel vm)
        : base(viewport)
    {
        _viewport = viewport;
        _vm       = vm;

        IsHitTestVisible = true;

        // Subscribe to viewport layout changes to invalidate decorations.
        _vm.PropertyChanged += (_, _) => InvalidateVisual();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Replaces the current set of links and triggers a redraw.
    /// </summary>
    public void SetLinks(IReadOnlyList<TextLink> links)
    {
        _links = links;
        InvalidateVisual();
    }

    // ── Rendering ─────────────────────────────────────────────────────────────

    protected override void OnRender(DrawingContext dc)
    {
        if (_links.Count == 0) return;

        var brush = ResolveUnderlineBrush();
        var pen   = new Pen(brush, 1.0);
        pen.Freeze();

        var lineHeight = _viewport.LineHeight;
        var charWidth  = _viewport.CharWidth;
        var lineNumW   = _viewport.LineNumberColumnWidth;
        var firstLine  = _viewport.FirstVisibleLine;
        var horizOff   = _viewport.HorizontalOffset;

        var lines = _vm.Lines;
        if (lines is null || lines.Count == 0) return;

        // Build per-line start-offset table (0-based cumulative char offsets).
        // Only build up to the visible range + a small buffer.
        var totalLines  = lines.Count;
        var visibleEnd  = Math.Min(totalLines, firstLine + (int)(_viewport.ActualHeight / lineHeight) + 2);

        // Cumulative offsets: lineStarts[i] = char offset of start of line i.
        Span<int> lineStarts = totalLines <= 1024
            ? stackalloc int[totalLines + 1]
            : new int[totalLines + 1];

        lineStarts[0] = 0;
        for (var i = 0; i < totalLines; i++)
            lineStarts[i + 1] = lineStarts[i] + lines[i].Length + 1; // +1 for '\n'

        foreach (var link in _links)
        {
            // Locate which line this link starts on.
            var line = BinarySearchLine(lineStarts, totalLines, link.StartOffset);
            if (line < firstLine || line >= visibleEnd) continue;

            var col       = link.StartOffset - lineStarts[line];
            var spanLen   = link.EndOffset - link.StartOffset;
            var x         = lineNumW + col * charWidth - horizOff;
            var y         = (line - firstLine) * lineHeight + lineHeight - 2; // just below text baseline
            var width     = spanLen * charWidth;

            if (x + width < 0 || x > _viewport.ActualWidth) continue;

            dc.DrawLine(pen, new Point(x, y), new Point(x + width, y));
        }
    }

    // ── Mouse events ──────────────────────────────────────────────────────────

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        var link = HitTestLink(e.GetPosition(_viewport));
        if (link != _hoveredLink)
        {
            _hoveredLink = link;
            Cursor = link is not null ? Cursors.Hand : null;
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _hoveredLink = null;
        Cursor       = null;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);

        // Only navigate on Ctrl+Click.
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;

        var link = HitTestLink(e.GetPosition(_viewport));
        if (link is null) return;

        e.Handled = true;
        link.OnClick();
    }

    // ── Hit testing ───────────────────────────────────────────────────────────

    private TextLink? HitTestLink(Point mousePos)
    {
        if (_links.Count == 0) return null;

        var lineHeight = _viewport.LineHeight;
        var charWidth  = _viewport.CharWidth;
        var lineNumW   = _viewport.LineNumberColumnWidth;
        var firstLine  = _viewport.FirstVisibleLine;
        var horizOff   = _viewport.HorizontalOffset;

        if (charWidth <= 0 || lineHeight <= 0) return null;

        var clickLine = firstLine + (int)(mousePos.Y / lineHeight);
        var clickCol  = (int)((mousePos.X - lineNumW + horizOff) / charWidth);

        if (clickLine < 0 || clickCol < 0) return null;

        var lines = _vm.Lines;
        if (lines is null || clickLine >= lines.Count) return null;

        // Build cumulative offset for clickLine.
        var offset = 0;
        for (var i = 0; i < clickLine; i++)
            offset += lines[i].Length + 1;
        offset += clickCol;

        foreach (var link in _links)
        {
            if (offset >= link.StartOffset && offset < link.EndOffset)
                return link;
        }

        return null;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Brush ResolveUnderlineBrush()
    {
        if (_underlineBrush is not null) return _underlineBrush;

        if (Application.Current?.TryFindResource("PFP_AccentBrush") is Brush themed)
        {
            _underlineBrush = themed;
            return themed;
        }

        _underlineBrush = FallbackUnderlineBrush;
        return _underlineBrush;
    }

    private static int BinarySearchLine(Span<int> lineStarts, int totalLines, int offset)
    {
        int lo = 0, hi = totalLines - 1;
        while (lo <= hi)
        {
            var mid = (lo + hi) / 2;
            if (lineStarts[mid] <= offset && (mid + 1 > totalLines || lineStarts[mid + 1] > offset))
                return mid;
            if (lineStarts[mid] > offset)
                hi = mid - 1;
            else
                lo = mid + 1;
        }
        return Math.Max(0, lo - 1);
    }
}
