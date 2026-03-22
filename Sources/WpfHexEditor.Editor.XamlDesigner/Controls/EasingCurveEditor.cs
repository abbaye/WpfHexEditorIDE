// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: EasingCurveEditor.cs
// Description:
//     200×200 Bezier easing curve editor for KeySpline / IEasingFunction.
//     Renders a cubic Bezier preview and allows the user to drag the two
//     control points (P1 and P2) to reshape the curve.
//     Fires CurveChanged when either control point is moved.
//
// Architecture Notes:
//     Standalone FrameworkElement. No XAML file.
//     Hit-test on the two control-point circles; drag routing via
//     MouseLeftButtonDown / Move / Up (no Thumb, to keep it lightweight).
//     Output: two normalized Points (0..1) matching WPF KeySpline format.
//     Theme-aware via XD_EasingCurveStroke and XD_EasingHandleBrush tokens.
// ==========================================================

using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace WpfHexEditor.Editor.XamlDesigner.Controls;

/// <summary>
/// Interactive Bezier easing curve editor.
/// </summary>
public sealed class EasingCurveEditor : FrameworkElement
{
    private const double HandleRadius = 6.0;
    private const double PadPx        = 16.0;   // padding inside the 200×200 area

    // Control points in normalized 0..1 space.
    private Point _p1 = new(0.25, 0.1);
    private Point _p2 = new(0.25, 1.0);

    // Drag state.
    private int   _dragging = -1;  // -1 none, 1 = P1, 2 = P2
    private Point _lastMouse;

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Fired when a control point is moved. Args are (P1, P2) in normalized space.</summary>
    public event EventHandler<(Point P1, Point P2)>? CurveChanged;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Sets the curve control points programmatically.</summary>
    public void SetCurve(Point p1, Point p2)
    {
        _p1 = p1;
        _p2 = p2;
        InvalidateVisual();
    }

    // ── Measure / Arrange ─────────────────────────────────────────────────────

    protected override Size MeasureOverride(Size constraint)
        => new(200, 200);

    // ── Rendering ─────────────────────────────────────────────────────────────

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth;
        double h = ActualHeight;

        var curveBrush  = Application.Current?.TryFindResource("XD_EasingCurveStroke") as Brush
                          ?? new SolidColorBrush(Color.FromRgb(0x4F, 0xC1, 0xFF));
        var handleBrush = Application.Current?.TryFindResource("XD_EasingHandleBrush") as Brush
                          ?? new SolidColorBrush(Color.FromRgb(0xFF, 0x79, 0xC6));

        // Background.
        dc.DrawRectangle(
            new SolidColorBrush(Color.FromArgb(220, 18, 18, 30)),
            null,
            new Rect(0, 0, w, h));

        // Grid lines.
        var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), 0.5);
        gridPen.Freeze();
        for (int i = 1; i < 4; i++)
        {
            double x = PadPx + (w - PadPx * 2) * i / 4.0;
            double y = PadPx + (h - PadPx * 2) * i / 4.0;
            dc.DrawLine(gridPen, new Point(x, PadPx), new Point(x, h - PadPx));
            dc.DrawLine(gridPen, new Point(PadPx, y), new Point(w - PadPx, y));
        }

        // Diagonal baseline.
        dc.DrawLine(gridPen, new Point(PadPx, h - PadPx), new Point(w - PadPx, PadPx));

        // Convert normalized → pixel.
        Point ToPixel(Point n) => new(
            PadPx + n.X * (w - PadPx * 2),
            (h - PadPx) - n.Y * (h - PadPx * 2));

        var p0px = new Point(PadPx, h - PadPx);
        var p1px = ToPixel(_p1);
        var p2px = ToPixel(_p2);
        var p3px = new Point(w - PadPx, PadPx);

        // Control point tangent lines.
        var tangentPen = new Pen(handleBrush, 1.0) { DashStyle = DashStyles.Dot };
        tangentPen.Freeze();
        dc.DrawLine(tangentPen, p0px, p1px);
        dc.DrawLine(tangentPen, p3px, p2px);

        // Cubic Bezier curve.
        var geom = new PathGeometry();
        var fig  = new PathFigure { StartPoint = p0px };
        fig.Segments.Add(new BezierSegment(p1px, p2px, p3px, isStroked: true));
        geom.Figures.Add(fig);
        var curvePen = new Pen(curveBrush, 2.0);
        curvePen.Freeze();
        dc.DrawGeometry(null, curvePen, geom);

        // Control-point handles.
        var strokePen = new Pen(Brushes.White, 1.0);
        strokePen.Freeze();
        dc.DrawEllipse(handleBrush, strokePen, p1px, HandleRadius, HandleRadius);
        dc.DrawEllipse(handleBrush, strokePen, p2px, HandleRadius, HandleRadius);

        // Anchor points.
        dc.DrawEllipse(curveBrush, null, p0px, 3, 3);
        dc.DrawEllipse(curveBrush, null, p3px, 3, 3);
    }

    // ── Mouse interaction ─────────────────────────────────────────────────────

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        var pos    = e.GetPosition(this);
        var p1px   = NormToPixel(_p1);
        var p2px   = NormToPixel(_p2);

        if (Distance(pos, p1px) <= HandleRadius + 4)
        {
            _dragging  = 1;
            _lastMouse = pos;
            CaptureMouse();
            e.Handled = true;
        }
        else if (Distance(pos, p2px) <= HandleRadius + 4)
        {
            _dragging  = 2;
            _lastMouse = pos;
            CaptureMouse();
            e.Handled = true;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragging < 0) return;

        var pos = e.GetPosition(this);
        var n   = PixelToNorm(pos);

        if (_dragging == 1) _p1 = Clamp01(n);
        else                _p2 = Clamp01(n);

        InvalidateVisual();
        CurveChanged?.Invoke(this, (_p1, _p2));
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (_dragging >= 0)
        {
            _dragging = -1;
            ReleaseMouseCapture();
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Point NormToPixel(Point n)
    {
        double w = ActualWidth;
        double h = ActualHeight;
        return new Point(PadPx + n.X * (w - PadPx * 2), (h - PadPx) - n.Y * (h - PadPx * 2));
    }

    private Point PixelToNorm(Point px)
    {
        double w = ActualWidth;
        double h = ActualHeight;
        return new Point(
            (px.X - PadPx) / (w - PadPx * 2),
            1.0 - (px.Y - PadPx) / (h - PadPx * 2));
    }

    private static double Distance(Point a, Point b)
    {
        double dx = a.X - b.X;
        double dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static Point Clamp01(Point p)
        => new(Math.Clamp(p.X, 0, 1), Math.Clamp(p.Y, 0, 1));
}
