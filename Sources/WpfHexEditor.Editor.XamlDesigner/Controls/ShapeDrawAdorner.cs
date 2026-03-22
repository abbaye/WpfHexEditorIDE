// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: ShapeDrawAdorner.cs
// Description:
//     Live preview adorner shown during a shape draw drag on the canvas.
//     Renders a translucent outline of the shape being drawn (Rectangle,
//     Ellipse, or Line) following the mouse, then fires DrawCompleted when
//     the user releases the mouse button.
//
// Architecture Notes:
//     Adorner on DesignRoot. Non-hit-testable (mouse events handled by
//     DesignCanvas, which calls Update() and Complete() directly).
//     Theme-aware via XD_DrawPreviewStrokeBrush and XD_DrawPreviewFillBrush.
// ==========================================================

using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using WpfHexEditor.Editor.XamlDesigner.Services;

namespace WpfHexEditor.Editor.XamlDesigner.Controls;

/// <summary>
/// Translucent shape preview drawn during a draw-tool drag.
/// </summary>
public sealed class ShapeDrawAdorner : Adorner
{
    private DrawingTool _tool;
    private Point       _start;
    private Point       _current;

    public ShapeDrawAdorner(UIElement adornedElement) : base(adornedElement)
    {
        IsHitTestVisible = false;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Starts a new draw preview from <paramref name="start"/>.</summary>
    public void Begin(DrawingTool tool, Point start)
    {
        _tool    = tool;
        _start   = start;
        _current = start;
        InvalidateVisual();
    }

    /// <summary>Updates the live preview to the current mouse position.</summary>
    public void Update(Point current)
    {
        _current = current;
        InvalidateVisual();
    }

    /// <summary>Returns the final bounding rect in adorner coordinate space.</summary>
    public Rect GetBounds()
    {
        double x = Math.Min(_start.X, _current.X);
        double y = Math.Min(_start.Y, _current.Y);
        double w = Math.Abs(_current.X - _start.X);
        double h = Math.Abs(_current.Y - _start.Y);
        return new Rect(x, y, Math.Max(1, w), Math.Max(1, h));
    }

    // ── Rendering ─────────────────────────────────────────────────────────────

    protected override void OnRender(DrawingContext dc)
    {
        if (_tool == DrawingTool.None) return;

        var strokeBrush = Application.Current?.TryFindResource("XD_DrawPreviewStrokeBrush") as Brush
                          ?? new SolidColorBrush(Color.FromArgb(200, 0, 122, 204));
        var fillBrush   = Application.Current?.TryFindResource("XD_DrawPreviewFillBrush") as Brush
                          ?? new SolidColorBrush(Color.FromArgb(40, 0, 122, 204));

        var pen = new Pen(strokeBrush, 1.5) { DashStyle = DashStyles.Dash };
        pen.Freeze();

        var bounds = GetBounds();

        switch (_tool)
        {
            case DrawingTool.Rectangle:
                dc.DrawRectangle(fillBrush, pen, bounds);
                break;

            case DrawingTool.Ellipse:
                dc.DrawEllipse(fillBrush, pen,
                    new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2),
                    bounds.Width / 2, bounds.Height / 2);
                break;

            case DrawingTool.Line:
                dc.DrawLine(pen, _start, _current);
                break;
        }

        // Dimension label.
        if (bounds.Width > 20 && bounds.Height > 20 && _tool != DrawingTool.Line)
        {
            var ft = new System.Windows.Media.FormattedText(
                $"{Math.Round(bounds.Width)} × {Math.Round(bounds.Height)}",
                System.Globalization.CultureInfo.CurrentUICulture,
                System.Windows.FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                9.0, strokeBrush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            dc.DrawText(ft, new Point(bounds.X + 4, bounds.Y + 4));
        }
    }
}
