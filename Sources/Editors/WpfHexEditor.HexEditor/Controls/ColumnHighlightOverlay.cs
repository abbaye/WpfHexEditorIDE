// ==========================================================
// Project: WpfHexEditor.HexEditor
// File: Controls/ColumnHighlightOverlay.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-29
// Description:
//     Lightweight overlay that renders three optional highlights:
//       • Vertical column stripe in the hex panel (active byte column)
//       • Vertical column stripe in the ASCII panel (same column)
//       • Horizontal row stripe (active line highlight)
//     All stripes are clamped to the visible content height so they
//     do not bleed into the empty space below the last rendered line.
//
// Architecture Notes:
//     DrawingContext-based rendering (no child UIElements).
//     IsHitTestVisible=false — all mouse events pass through.
//     Updated by HexEditor.ColumnHighlight.cs partial when cursor moves.
// ==========================================================

using System;
using System.Windows;
using System.Windows.Media;

namespace WpfHexEditor.HexEditor.Controls;

/// <summary>
/// Semi-transparent overlay that highlights the active column (hex + ASCII)
/// and optionally the active row in the hex viewport.
/// </summary>
public sealed class ColumnHighlightOverlay : FrameworkElement
{
    // ── State ─────────────────────────────────────────────────────────────────

    private bool   _visible;

    // Column (hex panel)
    private double _colX;
    private double _colWidth;

    // Column (ASCII panel) — negative X means not shown
    private double _asciiX;
    private double _asciiWidth;

    // Row highlight
    private bool   _showRow;
    private double _rowY;
    private double _rowHeight;

    // Height clamp: render stripes only up to the last visible line
    private double _visibleContentHeight;

    // ── Brushes ───────────────────────────────────────────────────────────────

    /// <summary>Column stripe — semi-transparent blue (~13% opacity).</summary>
    private static readonly Brush _colBrush;

    /// <summary>Row stripe — lighter semi-transparent blue (~8% opacity).</summary>
    private static readonly Brush _rowBrush;

    static ColumnHighlightOverlay()
    {
        _colBrush = new SolidColorBrush(Color.FromArgb(0x28, 0x00, 0x78, 0xD4));
        _colBrush.Freeze();
        _rowBrush = new SolidColorBrush(Color.FromArgb(0x14, 0x00, 0x78, 0xD4));
        _rowBrush.Freeze();
    }

    public ColumnHighlightOverlay()
    {
        IsHitTestVisible = false;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Updates and shows all active highlight elements.
    /// </summary>
    /// <param name="columnIndex">0-based byte column within the visible row.</param>
    /// <param name="hexPanelStartX">X where the hex panel starts (in overlay coordinates).</param>
    /// <param name="cellWidth">Width of one hex byte cell (in overlay coordinates).</param>
    /// <param name="spacerOffset">Extra X offset from byte-group spacers.</param>
    /// <param name="visibleContentHeight">Height of the visible content area (clamps stripe height).</param>
    /// <param name="asciiPanelStartX">X where the ASCII panel starts; &lt;0 to hide ASCII stripe.</param>
    /// <param name="asciiCharWidth">Width of one ASCII character; ≤0 to hide ASCII stripe.</param>
    /// <param name="rowY">Top Y of the current row (for row highlight); negative to hide.</param>
    /// <param name="rowHeight">Height of the current row.</param>
    /// <param name="showRow">Whether to draw the row highlight.</param>
    public void SetColumn(
        int    columnIndex,
        double hexPanelStartX,
        double cellWidth,
        double spacerOffset,
        double visibleContentHeight,
        double asciiPanelStartX,
        double asciiCharWidth,
        double rowY,
        double rowHeight,
        bool   showRow)
    {
        bool hasHexCol   = columnIndex >= 0 && cellWidth > 0;
        bool hasAsciiCol = asciiPanelStartX >= 0 && asciiCharWidth > 0;
        bool hasRow      = showRow && rowY >= 0 && rowHeight > 0;

        if (!hasHexCol && !hasAsciiCol && !hasRow)
        {
            Hide();
            return;
        }

        if (hasHexCol)
        {
            _colX    = hexPanelStartX + columnIndex * cellWidth + spacerOffset;
            _colWidth = cellWidth;
        }
        else
        {
            _colWidth = 0;
        }

        _visibleContentHeight = visibleContentHeight;

        // ASCII column — shown only when ASCII panel is visible
        if (asciiPanelStartX >= 0 && asciiCharWidth > 0)
        {
            _asciiX     = asciiPanelStartX + columnIndex * asciiCharWidth;
            _asciiWidth = asciiCharWidth;
        }
        else
        {
            _asciiX    = -1;
            _asciiWidth = 0;
        }

        _showRow   = showRow;
        _rowY      = rowY;
        _rowHeight  = rowHeight;

        _visible = true;
        InvalidateVisual();
    }

    /// <summary>Hides all highlight elements.</summary>
    public void Hide()
    {
        if (!_visible) return;
        _visible = false;
        InvalidateVisual();
    }

    // ── Rendering ─────────────────────────────────────────────────────────────

    protected override void OnRender(DrawingContext dc)
    {
        if (!_visible) return;

        // Clamp stripe height to visible content (never bleed past last rendered line)
        double maxH = _visibleContentHeight > 0
            ? Math.Min(_visibleContentHeight, ActualHeight)
            : ActualHeight;

        // 1. Row highlight (drawn first — behind columns)
        if (_showRow && _rowHeight > 0 && _rowY >= 0 && _rowY < maxH)
        {
            double rowBottom = Math.Min(_rowY + _rowHeight, maxH);
            dc.DrawRectangle(_rowBrush, null,
                new Rect(0, _rowY, ActualWidth, rowBottom - _rowY));
        }

        // 2. Hex column stripe
        if (_colWidth > 0)
            dc.DrawRectangle(_colBrush, null, new Rect(_colX, 0, _colWidth, maxH));

        // 3. ASCII column stripe
        if (_asciiX >= 0 && _asciiWidth > 0)
            dc.DrawRectangle(_colBrush, null, new Rect(_asciiX, 0, _asciiWidth, maxH));
    }
}
