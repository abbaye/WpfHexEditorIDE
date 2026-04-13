// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: Controls/DesignSelectionLayer.cs
// Description:
//     Lightweight DrawingVisual-based overlay that renders selection
//     borders, resize handles, hover pre-selection, and rubber-band
//     on top of the design canvas — without using WPF Adorners.
//
// Architecture Notes:
//     Pattern: Custom Visual Tree (VisualChildrenCount + GetVisualChild).
//     Three DrawingVisual children, ordered from bottom to top:
//       1. _selectionVisual  — solid blue border + 8 handles
//       2. _hoverVisual      — dashed semi-opaque border (pre-selection)
//       3. _rubberBandVisual — translucent drag marquee
//     IsHitTestVisible = false: all mouse events go through to canvas.
//     DynamicResource tokens (XD_SelectionBorderBrush, XD_HoverBorderBrush)
//     resolved via TryFindResource — fallback to VS-blue if absent.
// ==========================================================

using System.Windows;
using System.Windows.Media;

namespace WpfHexEditor.Editor.XamlDesigner.Controls;

/// <summary>
/// Transparent overlay that draws selection, hover, and rubber-band visuals
/// via <see cref="DrawingVisual"/> children instead of WPF Adorners.
/// </summary>
internal sealed class DesignSelectionLayer : FrameworkElement
{
    // ── Constants ────────────────────────────────────────────────────────────

    private const double HandlePx     = 5.0;
    private const double SelectionPen = 1.5;
    private const double HoverPen     = 1.0;

    // Fallback colours (VS-blue palette)
    private static readonly Color SelectionColor  = Color.FromRgb(0, 120, 215);
    private static readonly Color HoverColor      = Color.FromArgb(160, 0, 120, 215);
    private static readonly Color RubberFill      = Color.FromArgb(40,  0, 120, 215);
    private static readonly Color RubberBorder    = Color.FromArgb(200, 0, 120, 215);

    // ── Visual children ──────────────────────────────────────────────────────

    private readonly DrawingVisual _selectionVisual  = new();
    private readonly DrawingVisual _hoverVisual      = new();
    private readonly DrawingVisual _rubberBandVisual = new();

    // ── Static ctor — disable hit-testing for the entire class ───────────────

    static DesignSelectionLayer()
    {
        IsHitTestVisibleProperty.OverrideMetadata(
            typeof(DesignSelectionLayer),
            new UIPropertyMetadata(false));
    }

    // ── Ctor ─────────────────────────────────────────────────────────────────

    public DesignSelectionLayer()
    {
        AddVisualChild(_selectionVisual);
        AddVisualChild(_hoverVisual);
        AddVisualChild(_rubberBandVisual);
    }

    // ── Visual tree overrides ────────────────────────────────────────────────

    protected override int VisualChildrenCount => 3;

    protected override Visual GetVisualChild(int index) => index switch
    {
        0 => _selectionVisual,
        1 => _hoverVisual,
        2 => _rubberBandVisual,
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    };

    // ── Selection ────────────────────────────────────────────────────────────

    /// <summary>
    /// Draws a solid selection border + 8 resize handles for each rect in
    /// <paramref name="bounds"/>. Pass an empty enumerable to clear.
    /// </summary>
    public void DrawSelection(IEnumerable<Rect> bounds)
    {
        Brush borderBrush = TryFindResource("XD_SelectionBorderBrush") as Brush
            ?? new SolidColorBrush(SelectionColor);
        Brush handleFill  = TryFindResource("XD_SelectionHandleFill") as Brush
            ?? Brushes.White;

        var pen  = new Pen(borderBrush, SelectionPen) { DashStyle = DashStyles.Solid };
        var hPen = new Pen(borderBrush, 1.0);

        using var dc = _selectionVisual.RenderOpen();
        foreach (var r in bounds)
        {
            dc.DrawRectangle(null, pen, r);
            DrawHandles(dc, r, handleFill, hPen);
        }
    }

    /// <summary>Clears the selection visual.</summary>
    public void ClearSelection()
    {
        using var dc = _selectionVisual.RenderOpen();
        // empty — clears the visual
    }

    // ── Hover pre-selection ──────────────────────────────────────────────────

    /// <summary>
    /// Draws a dashed semi-opaque border for the element currently under the cursor
    /// (before any click). Call <see cref="ClearHover"/> when cursor leaves.
    /// </summary>
    public void DrawHover(Rect bounds)
    {
        Brush hoverBrush = TryFindResource("XD_HoverBorderBrush") as Brush
            ?? new SolidColorBrush(HoverColor);

        var pen = new Pen(hoverBrush, HoverPen)
        {
            DashStyle = new DashStyle([4.0, 3.0], 0)
        };

        using var dc = _hoverVisual.RenderOpen();
        dc.DrawRectangle(null, pen, bounds);
    }

    /// <summary>Clears the hover visual.</summary>
    public void ClearHover()
    {
        using var dc = _hoverVisual.RenderOpen();
        // empty
    }

    // ── Rubber-band ──────────────────────────────────────────────────────────

    /// <summary>
    /// Draws a translucent drag-selection marquee between <paramref name="start"/>
    /// and <paramref name="end"/> in canvas-local coordinates.
    /// </summary>
    public void DrawRubberBand(Point start, Point end)
    {
        var rect = new Rect(
            Math.Min(start.X, end.X), Math.Min(start.Y, end.Y),
            Math.Abs(end.X - start.X), Math.Abs(end.Y - start.Y));

        var fillBrush = new SolidColorBrush(RubberFill);
        fillBrush.Freeze();
        var pen = new Pen(new SolidColorBrush(RubberBorder), 1.0);
        pen.Freeze();

        using var dc = _rubberBandVisual.RenderOpen();
        dc.DrawRectangle(fillBrush, pen, rect);
    }

    /// <summary>Clears the rubber-band visual.</summary>
    public void ClearRubberBand()
    {
        using var dc = _rubberBandVisual.RenderOpen();
        // empty
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static void DrawHandles(DrawingContext dc, Rect r, Brush fill, Pen pen)
    {
        double l  = r.Left,       ri = r.Right;
        double t  = r.Top,        b  = r.Bottom;
        double mx = (l + ri) / 2, my = (t + b) / 2;
        double h  = HandlePx;

        void Handle(double cx, double cy) =>
            dc.DrawRectangle(fill, pen, new Rect(cx - h, cy - h, h * 2, h * 2));

        Handle(l,  t);  Handle(mx, t);  Handle(ri, t);
        Handle(ri, my);
        Handle(ri, b);  Handle(mx, b);  Handle(l,  b);
        Handle(l,  my);
    }
}
