// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram
// File: Controls/Adorners/RubberBandAdorner.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-19
// Description:
//     WPF Adorner that renders a dashed rubber-band selection rectangle
//     during a drag-select operation on the diagram canvas.
//
// Architecture Notes:
//     Pattern: Decorator (WPF Adorner).
//     OnRender uses a dashed Pen with DynamicResource tokens for
//     CD_RubberBandBorderBrush and CD_RubberBandFill.
//     StartPoint and EndPoint are set by the DiagramCanvas mouse
//     handlers; SelectionRect is the computed Rect used for hit-testing.
// ==========================================================

using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace WpfHexEditor.Editor.ClassDiagram.Controls.Adorners;

/// <summary>
/// Renders a dashed rubber-band selection rectangle over the diagram canvas.
/// </summary>
public sealed class RubberBandAdorner : Adorner
{
    private static readonly DashStyle DashedStyle = new([4, 4], 0);

    private Point _startPoint;
    private Point _endPoint;

    public RubberBandAdorner(UIElement adornedElement) : base(adornedElement)
    {
        IsHitTestVisible = false;
    }

    // ---------------------------------------------------------------------------
    // Properties
    // ---------------------------------------------------------------------------

    public Point StartPoint
    {
        get => _startPoint;
        set { _startPoint = value; InvalidateVisual(); }
    }

    public Point EndPoint
    {
        get => _endPoint;
        set { _endPoint = value; InvalidateVisual(); }
    }

    /// <summary>
    /// The normalized selection rectangle derived from StartPoint and EndPoint.
    /// Always returns a non-inverted Rect regardless of drag direction.
    /// </summary>
    public Rect SelectionRect => new(
        Math.Min(_startPoint.X, _endPoint.X),
        Math.Min(_startPoint.Y, _endPoint.Y),
        Math.Abs(_endPoint.X - _startPoint.X),
        Math.Abs(_endPoint.Y - _startPoint.Y));

    // ---------------------------------------------------------------------------
    // Rendering
    // ---------------------------------------------------------------------------

    protected override void OnRender(DrawingContext drawingContext)
    {
        Rect rect = SelectionRect;

        // Attempt to use theme tokens; fall back to semi-transparent colors
        Brush? fillBrush = TryFindResource("CD_RubberBandFill") as Brush
            ?? new SolidColorBrush(Color.FromArgb(30, 0, 120, 215));

        Brush? borderBrush = TryFindResource("CD_RubberBandBorderBrush") as Brush
            ?? new SolidColorBrush(Color.FromArgb(200, 0, 120, 215));

        var pen = new Pen(borderBrush, 1.0)
        {
            DashStyle = DashedStyle,
            DashCap   = PenLineCap.Flat
        };

        drawingContext.DrawRectangle(fillBrush, pen, rect);
    }
}
