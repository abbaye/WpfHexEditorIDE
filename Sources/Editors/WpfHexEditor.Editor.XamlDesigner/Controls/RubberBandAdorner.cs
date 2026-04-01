// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: RubberBandAdorner.cs
// Author: Derek Tremblay
// Created: 2026-03-17
// Description:
//     Adorner that draws a selection rectangle (rubber-band) as the user
//     drags to select multiple elements on the design canvas.
//     Purely decorative — non-hit-testable. Updated live via UpdateBounds().
//
// Architecture Notes:
//     Adorner on the DesignCanvas root. Created by DesignCanvas on mouse-down
//     in empty space, removed on mouse-up after selection is finalized.
// ==========================================================

using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace WpfHexEditor.Editor.XamlDesigner.Controls;

/// <summary>
/// Draws a dashed selection rectangle during rubber-band multi-select.
/// </summary>
public sealed class RubberBandAdorner : Adorner
{
    private Rect _bounds = Rect.Empty;

    public RubberBandAdorner(UIElement adornedElement) : base(adornedElement)
    {
        IsHitTestVisible = false;
    }

    /// <summary>Updates the displayed rectangle and triggers a redraw.</summary>
    public void UpdateBounds(Point start, Point current)
    {
        _bounds = new Rect(
            Math.Min(start.X, current.X),
            Math.Min(start.Y, current.Y),
            Math.Abs(current.X - start.X),
            Math.Abs(current.Y - start.Y));
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (_bounds.IsEmpty || _bounds.Width < 2 || _bounds.Height < 2) return;

        var fillBrush = Application.Current?.TryFindResource("XD_RubberBandFillBrush") as Brush
                        ?? new SolidColorBrush(Color.FromArgb(30, 0, 120, 215));

        var strokeBrush = Application.Current?.TryFindResource("XD_SelectionBorderBrush") as Brush
                          ?? new SolidColorBrush(Color.FromRgb(0, 122, 204));

        var pen = new Pen(strokeBrush, 1.0) { DashStyle = DashStyles.Dash };
        pen.Freeze();

        dc.DrawRectangle(fillBrush, pen, _bounds);
    }
}
