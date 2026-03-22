// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: TransformOriginAdorner.cs
// Description:
//     Adorner that shows a 3×3 dot grid for picking RenderTransformOrigin.
//     Displayed when the user activates the transform origin picker in the
//     Transform toolbar pod. Clicking a dot sets RenderTransformOrigin on
//     the adorned element and commits the change via DesignInteractionService.
//
// Architecture Notes:
//     Non-blocking adorner — sits above the ResizeAdorner layer.
//     The 9 dots map directly to the 9 WPF normalized points:
//         (0,0) (0.5,0) (1,0)
//         (0,0.5) (0.5,0.5) (1,0.5)
//         (0,1) (0.5,1) (1,1)
//     Hit-test active only on the dot circles.
// ==========================================================

using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace WpfHexEditor.Editor.XamlDesigner.Controls;

/// <summary>
/// Overlay adorner showing 9 transform-origin pick points on the selected element.
/// </summary>
public sealed class TransformOriginAdorner : Adorner
{
    private const double DotRadius   = 5.0;
    private const double DotSpacingX = 0.5; // normalized step (0, 0.5, 1)
    private const double DotSpacingY = 0.5;

    private static readonly Point[] NormPoints =
    [
        new(0.0, 0.0), new(0.5, 0.0), new(1.0, 0.0),
        new(0.0, 0.5), new(0.5, 0.5), new(1.0, 0.5),
        new(0.0, 1.0), new(0.5, 1.0), new(1.0, 1.0),
    ];

    private int _hoveredIndex = -1;

    public event EventHandler<Point>? OriginPicked;

    public TransformOriginAdorner(UIElement adornedElement) : base(adornedElement)
    {
        IsHitTestVisible = true;
        Cursor           = Cursors.Hand;
    }

    // ── Adorner overrides ─────────────────────────────────────────────────────

    protected override HitTestResult HitTestCore(PointHitTestParameters p)
    {
        var w = AdornedElement.RenderSize.Width;
        var h = AdornedElement.RenderSize.Height;

        foreach (var np in NormPoints)
        {
            var center = new Point(np.X * w, np.Y * h);
            double dx  = p.HitPoint.X - center.X;
            double dy  = p.HitPoint.Y - center.Y;
            if (Math.Sqrt(dx * dx + dy * dy) <= DotRadius + 2)
                return new PointHitTestResult(this, p.HitPoint);
        }
        return base.HitTestCore(p);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var dotBrush    = Application.Current?.TryFindResource("XD_TransformOriginDotBrush")    as Brush
                          ?? new SolidColorBrush(Color.FromArgb(180, 80, 80, 80));
        var activeBrush = Application.Current?.TryFindResource("XD_TransformOriginActiveBrush") as Brush
                          ?? new SolidColorBrush(Color.FromRgb(0x4F, 0xC1, 0xFF));
        var strokeBrush = new SolidColorBrush(Colors.White) { Opacity = 0.8 };
        strokeBrush.Freeze();
        var pen = new Pen(strokeBrush, 1.0);
        pen.Freeze();

        var w = AdornedElement.RenderSize.Width;
        var h = AdornedElement.RenderSize.Height;

        // Current origin dot (highlight)
        var currentOrigin = (AdornedElement as FrameworkElement)?.RenderTransformOrigin
                            ?? new Point(0.5, 0.5);

        for (int i = 0; i < NormPoints.Length; i++)
        {
            var np     = NormPoints[i];
            var center = new Point(np.X * w, np.Y * h);

            bool isActive  = Math.Abs(np.X - currentOrigin.X) < 0.01 &&
                             Math.Abs(np.Y - currentOrigin.Y) < 0.01;
            bool isHovered = i == _hoveredIndex;

            double radius = (isActive || isHovered) ? DotRadius + 1 : DotRadius - 1;
            var    fill   = (isActive || isHovered) ? activeBrush : dotBrush;

            dc.DrawEllipse(fill, pen, center, radius, radius);
        }
    }

    // ── Mouse interaction ─────────────────────────────────────────────────────

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int newHover = GetDotIndexAt(e.GetPosition(this));
        if (newHover != _hoveredIndex)
        {
            _hoveredIndex = newHover;
            InvalidateVisual();
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _hoveredIndex = -1;
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        int idx = GetDotIndexAt(e.GetPosition(this));
        if (idx < 0) return;

        var np = NormPoints[idx];
        if (AdornedElement is FrameworkElement fe)
            fe.RenderTransformOrigin = np;

        OriginPicked?.Invoke(this, np);
        InvalidateVisual();
        e.Handled = true;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private int GetDotIndexAt(Point pt)
    {
        var w = AdornedElement.RenderSize.Width;
        var h = AdornedElement.RenderSize.Height;

        for (int i = 0; i < NormPoints.Length; i++)
        {
            var np     = NormPoints[i];
            var center = new Point(np.X * w, np.Y * h);
            double dx  = pt.X - center.X;
            double dy  = pt.Y - center.Y;
            if (Math.Sqrt(dx * dx + dy * dy) <= DotRadius + 4)
                return i;
        }
        return -1;
    }
}
