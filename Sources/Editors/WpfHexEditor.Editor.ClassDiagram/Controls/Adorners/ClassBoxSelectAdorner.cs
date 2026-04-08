// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram
// File: Controls/Adorners/ClassBoxSelectAdorner.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-19
// Description:
//     WPF Adorner that renders a selection outline with 8 resize handles
//     (NW, N, NE, E, SE, S, SW, W) around a selected class box.
//
// Architecture Notes:
//     Pattern: Decorator (WPF Adorner).
//     Handle size is fixed at 6x6 logical pixels.
//     AdornedBounds is set by DiagramCanvas to the class box bounds
//     in adorner-layer coordinates.
//     Colors use CD_SelectionBorderBrush and CD_SelectionHandleFill tokens.
// ==========================================================

using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace WpfHexEditor.Editor.ClassDiagram.Controls.Adorners;

/// <summary>
/// Selection adorner with 8 resize handles drawn around a class box.
/// </summary>
public sealed class ClassBoxSelectAdorner : Adorner
{
    private const double HandleSize = 6.0;
    private const double HalfHandle = HandleSize / 2.0;

    private Rect _adornedBounds = Rect.Empty;

    public ClassBoxSelectAdorner(UIElement adornedElement) : base(adornedElement)
    {
        IsHitTestVisible = false;
    }

    // ---------------------------------------------------------------------------
    // Properties
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Bounds of the adorned class box in the adorner layer's coordinate space.
    /// Setting this triggers a redraw.
    /// </summary>
    public Rect AdornedBounds
    {
        get => _adornedBounds;
        set { _adornedBounds = value; InvalidateVisual(); }
    }

    // ---------------------------------------------------------------------------
    // Rendering
    // ---------------------------------------------------------------------------

    protected override void OnRender(DrawingContext drawingContext)
    {
        if (_adornedBounds == Rect.Empty) return;

        // The AdornerLayer sits in AdornerDecorator coordinate space, ABOVE the
        // ZoomPanCanvas RenderTransform (scale + translate). We must map the
        // diagram-local bounds through the full transform chain so the selection
        // rectangle tracks the node correctly at any zoom level and pan position.
        var adornerLayer = AdornerLayer.GetAdornerLayer(AdornedElement);
        Rect bounds = _adornedBounds;
        if (adornerLayer is not null)
        {
            try
            {
                GeneralTransform gt = AdornedElement.TransformToAncestor(adornerLayer);
                bounds = gt.TransformBounds(_adornedBounds);
            }
            catch { /* visual tree not ready — fall back to diagram-local coords */ }
        }

        Brush? borderBrush = TryFindResource("CD_SelectionBorderBrush") as Brush
            ?? new SolidColorBrush(Color.FromRgb(0, 120, 215));

        Brush? handleFill = TryFindResource("CD_SelectionHandleFill") as Brush
            ?? Brushes.White;

        var selectionPen = new Pen(borderBrush, 1.5);
        var handlePen    = new Pen(borderBrush, 1.0);

        // Selection border
        drawingContext.DrawRectangle(null, selectionPen, bounds);

        // 8 handle positions — must scale handle size with zoom so handles stay 6px on screen
        double zoom = bounds.Width > 0 && _adornedBounds.Width > 0
            ? bounds.Width / _adornedBounds.Width
            : 1.0;
        var handles = ComputeHandleRects(bounds, zoom);
        foreach (Rect handle in handles)
            drawingContext.DrawRectangle(handleFill, handlePen, handle);
    }

    // ---------------------------------------------------------------------------
    // Handle computation
    // ---------------------------------------------------------------------------

    private static IEnumerable<Rect> ComputeHandleRects(Rect bounds, double zoom)
    {
        double left   = bounds.Left;
        double right  = bounds.Right;
        double top    = bounds.Top;
        double bottom = bounds.Bottom;
        double midX   = (left + right) / 2.0;
        double midY   = (top + bottom) / 2.0;
        double halfH  = HalfHandle;   // handles stay fixed-size in screen px

        yield return HandleAt(left,  top,    halfH);   // NW
        yield return HandleAt(midX,  top,    halfH);   // N
        yield return HandleAt(right, top,    halfH);   // NE
        yield return HandleAt(right, midY,   halfH);   // E
        yield return HandleAt(right, bottom, halfH);   // SE
        yield return HandleAt(midX,  bottom, halfH);   // S
        yield return HandleAt(left,  bottom, halfH);   // SW
        yield return HandleAt(left,  midY,   halfH);   // W
    }

    private static Rect HandleAt(double cx, double cy, double half) =>
        new(cx - half, cy - half, half * 2, half * 2);
}
