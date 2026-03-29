// ==========================================================
// Project: WpfHexEditor.HexEditor
// File: Controls/ColumnHighlightOverlay.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-29
// Description:
//     Lightweight semi-transparent vertical stripe that highlights the
//     active byte column in the hex viewport.  IsHitTestVisible=false
//     so all mouse events pass through to the viewport below.
//
// Architecture Notes:
//     DrawingContext-based rendering (no child UIElements).
//     Positioned as a direct child of the HexEditor content Grid.
//     Updated by HexEditor.ColumnHighlight.cs partial when cursor moves.
//     Uses HexEditor_SelectionFirstColor (semi-transparent) as highlight brush.
// ==========================================================

using System.Windows;
using System.Windows.Media;

namespace WpfHexEditor.HexEditor.Controls;

/// <summary>
/// Semi-transparent column highlight overlay for the hex viewport.
/// </summary>
public sealed class ColumnHighlightOverlay : FrameworkElement
{
    // ── State ─────────────────────────────────────────────────────────────────
    private bool   _visible;
    private double _x;
    private double _width;

    private static readonly Brush _fillBrush;

    static ColumnHighlightOverlay()
    {
        _fillBrush = new SolidColorBrush(Color.FromArgb(0x22, 0x00, 0x78, 0xD4)); // ~13% blue
        _fillBrush.Freeze();
    }

    public ColumnHighlightOverlay()
    {
        IsHitTestVisible = false;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Recomputes and shows the column stripe.
    /// </summary>
    /// <param name="columnIndex">0-based byte column within the visible row.</param>
    /// <param name="hexPanelStartX">X offset where the hex panel starts (offset column width).</param>
    /// <param name="cellWidth">Width of one hex cell (includes spacing).</param>
    public void SetColumn(int columnIndex, double hexPanelStartX, double cellWidth, double spacerOffset = 0)
    {
        if (columnIndex < 0 || cellWidth <= 0)
        {
            Hide();
            return;
        }

        _x       = hexPanelStartX + columnIndex * cellWidth + spacerOffset;
        _width   = cellWidth;
        _visible = true;
        InvalidateVisual();
    }

    /// <summary>Hides the column highlight.</summary>
    public void Hide()
    {
        if (!_visible) return;
        _visible = false;
        InvalidateVisual();
    }

    // ── Rendering ─────────────────────────────────────────────────────────────

    protected override void OnRender(DrawingContext dc)
    {
        if (!_visible || _width <= 0) return;

        var rect = new Rect(_x, 0, _width, ActualHeight);
        dc.DrawRectangle(_fillBrush, null, rect);
    }
}
