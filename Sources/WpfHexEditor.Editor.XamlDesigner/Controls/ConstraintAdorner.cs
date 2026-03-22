// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: ConstraintAdorner.cs
// Description:
//     Blend-style constraint adorner. Draws 4 spring/pin icons on the
//     Left / Top / Right / Bottom edges of the adorned element.
//     Clicking a spring icon toggles the pin state via ConstraintService
//     and fires PinToggled so DesignCanvas can commit the change to XAML.
//
// Architecture Notes:
//     VisualCollection of 4 ToggleButton-like hit zones drawn in OnRender.
//     Uses PointHitTestParameters to route clicks to the correct edge.
//     Theme-aware: XD_ConstraintPinnedBrush / XD_ConstraintUnpinnedBrush.
// ==========================================================

using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.Editor.XamlDesigner.Services;

namespace WpfHexEditor.Editor.XamlDesigner.Controls;

/// <summary>
/// Overlay adorner showing 4 pin springs for layout-constraint control.
/// </summary>
public sealed class ConstraintAdorner : Adorner
{
    private const double SpringSize = 12.0;
    private const double SpringGap  = 6.0;    // gap between element edge and spring icon center

    private PinnedEdges _pins;

    /// <summary>Fired when the user clicks a pin spring. Arg is the toggled edge.</summary>
    public event EventHandler<PinnedEdges>? PinToggled;

    public ConstraintAdorner(UIElement adornedElement, PinnedEdges initialPins)
        : base(adornedElement)
    {
        _pins            = initialPins;
        IsHitTestVisible = true;
        Cursor           = Cursors.Hand;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Updates the displayed pin state.</summary>
    public void Refresh(PinnedEdges pins)
    {
        _pins = pins;
        InvalidateVisual();
    }

    // ── Adorner overrides ─────────────────────────────────────────────────────

    protected override HitTestResult HitTestCore(PointHitTestParameters p)
    {
        // Expand hit area to cover the spring icons outside the element bounds.
        const double extra = SpringSize + SpringGap;
        var expanded = new Rect(
            -extra,
            -extra,
            AdornedElement.RenderSize.Width  + extra * 2,
            AdornedElement.RenderSize.Height + extra * 2);

        return expanded.Contains(p.HitPoint)
            ? new PointHitTestResult(this, p.HitPoint)
            : base.HitTestCore(p);
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = AdornedElement.RenderSize.Width;
        double h = AdornedElement.RenderSize.Height;

        var pinnedBrush   = Application.Current?.TryFindResource("XD_ConstraintPinnedBrush")   as Brush
                            ?? new SolidColorBrush(Color.FromRgb(0x00, 0x7A, 0xCC));
        var unpinnedBrush = Application.Current?.TryFindResource("XD_ConstraintUnpinnedBrush") as Brush
                            ?? new SolidColorBrush(Color.FromArgb(120, 150, 150, 150));

        DrawSpring(dc, GetSpringCenter(PinnedEdges.Left,   w, h), PinnedEdges.Left,   true,  pinnedBrush, unpinnedBrush);
        DrawSpring(dc, GetSpringCenter(PinnedEdges.Top,    w, h), PinnedEdges.Top,    false, pinnedBrush, unpinnedBrush);
        DrawSpring(dc, GetSpringCenter(PinnedEdges.Right,  w, h), PinnedEdges.Right,  true,  pinnedBrush, unpinnedBrush);
        DrawSpring(dc, GetSpringCenter(PinnedEdges.Bottom, w, h), PinnedEdges.Bottom, false, pinnedBrush, unpinnedBrush);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        var pos = e.GetPosition(this);
        var hit = GetHitEdge(pos);
        if (hit != PinnedEdges.None)
        {
            PinToggled?.Invoke(this, hit);
            e.Handled = true;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Point GetSpringCenter(PinnedEdges edge, double w, double h) => edge switch
    {
        PinnedEdges.Left   => new Point(-(SpringGap + SpringSize / 2),  h / 2),
        PinnedEdges.Right  => new Point(w + SpringGap + SpringSize / 2, h / 2),
        PinnedEdges.Top    => new Point(w / 2, -(SpringGap + SpringSize / 2)),
        PinnedEdges.Bottom => new Point(w / 2,  h + SpringGap + SpringSize / 2),
        _                  => new Point(0, 0)
    };

    private PinnedEdges GetHitEdge(Point pos)
    {
        double w = AdornedElement.RenderSize.Width;
        double h = AdornedElement.RenderSize.Height;
        double r = SpringSize / 2.0 + 4;

        foreach (var edge in new[] { PinnedEdges.Left, PinnedEdges.Top, PinnedEdges.Right, PinnedEdges.Bottom })
        {
            var center = GetSpringCenter(edge, w, h);
            double dx  = pos.X - center.X;
            double dy  = pos.Y - center.Y;
            if (Math.Sqrt(dx * dx + dy * dy) <= r)
                return edge;
        }
        return PinnedEdges.None;
    }

    private void DrawSpring(
        DrawingContext dc, Point center, PinnedEdges edge,
        bool isHorizontal, Brush pinned, Brush unpinned)
    {
        bool isPinned = _pins.HasFlag(edge);
        var  brush    = isPinned ? pinned : unpinned;
        var  pen      = new Pen(brush, 1.5);
        pen.Freeze();

        double half = SpringSize / 2.0;

        if (isPinned)
        {
            // Solid filled square (pinned).
            dc.DrawRectangle(brush, null,
                new Rect(center.X - half * 0.6, center.Y - half * 0.6,
                         SpringSize * 0.6, SpringSize * 0.6));
        }
        else
        {
            // Dashed spring coil (unpinned).
            if (isHorizontal)
            {
                dc.DrawLine(pen, new Point(center.X - half, center.Y),
                                 new Point(center.X + half, center.Y));
                dc.DrawLine(pen, new Point(center.X - half * 0.5, center.Y - 3),
                                 new Point(center.X - half * 0.5, center.Y + 3));
                dc.DrawLine(pen, new Point(center.X + half * 0.5, center.Y - 3),
                                 new Point(center.X + half * 0.5, center.Y + 3));
            }
            else
            {
                dc.DrawLine(pen, new Point(center.X, center.Y - half),
                                 new Point(center.X, center.Y + half));
                dc.DrawLine(pen, new Point(center.X - 3, center.Y - half * 0.5),
                                 new Point(center.X + 3, center.Y - half * 0.5));
                dc.DrawLine(pen, new Point(center.X - 3, center.Y + half * 0.5),
                                 new Point(center.X + 3, center.Y + half * 0.5));
            }
        }
    }
}
