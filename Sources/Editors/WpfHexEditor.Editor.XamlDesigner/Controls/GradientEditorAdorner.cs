// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: GradientEditorAdorner.cs
// Description:
//     Inline gradient stop editor adorner. Renders a horizontal gradient
//     preview strip just below the adorned element. Up to 8 gradient stops
//     are shown as draggable diamond markers. Clicking an empty area of the
//     strip adds a new stop; clicking an existing stop selects it; dragging
//     moves it; right-clicking removes it. Fires StopsChanged whenever the
//     stop collection is modified.
//
// Architecture Notes:
//     Standalone Adorner — no XAML file.
//     All rendering done in OnRender; mouse routing via
//     OnMouseLeftButtonDown / Move / Up / RightButtonDown.
//     Outputs: IReadOnlyList<GradientStopInfo> sorted by offset.
//     Theme-aware: XD_GradientEditorLineBrush / XD_GradientStopSelectedBrush.
// ==========================================================

using System.Collections.Generic;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace WpfHexEditor.Editor.XamlDesigner.Controls;

/// <summary>Lightweight value type describing a single gradient stop.</summary>
public readonly record struct GradientStopInfo(Color Color, double Offset);

/// <summary>
/// Adorner that renders an inline gradient preview strip with draggable stops.
/// </summary>
public sealed class GradientEditorAdorner : Adorner
{
    private const double StripHeight  = 16.0;
    private const double StripGap     = 6.0;   // gap below element bottom edge
    private const double DiamondHalf  = 6.0;
    private const double DiamondVPos  = StripGap + StripHeight + 4.0;  // Y for diamond tips

    // ── State ─────────────────────────────────────────────────────────────────

    private readonly List<GradientStopInfo> _stops = new();
    private int    _selectedIdx  = -1;
    private int    _draggingIdx  = -1;
    private double _stripLeft;
    private double _stripWidth;

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Fired when the stop collection changes. Arg is sorted stops list.</summary>
    public event EventHandler<IReadOnlyList<GradientStopInfo>>? StopsChanged;

    // ── Construction ──────────────────────────────────────────────────────────

    public GradientEditorAdorner(UIElement adornedElement, IEnumerable<GradientStopInfo>? initialStops = null)
        : base(adornedElement)
    {
        IsHitTestVisible = true;
        Cursor           = Cursors.Hand;

        if (initialStops is not null)
            _stops.AddRange(initialStops);
        else
        {
            _stops.Add(new GradientStopInfo(Colors.Black,  0.0));
            _stops.Add(new GradientStopInfo(Colors.White,  1.0));
        }

        SortStops();
        _selectedIdx = 0;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Replaces all stops and redraws.</summary>
    public void SetStops(IEnumerable<GradientStopInfo> stops)
    {
        _stops.Clear();
        _stops.AddRange(stops);
        SortStops();
        _selectedIdx = _stops.Count > 0 ? 0 : -1;
        InvalidateVisual();
    }

    // ── Adorner overrides ─────────────────────────────────────────────────────

    protected override HitTestResult HitTestCore(PointHitTestParameters p)
    {
        double w = AdornedElement.RenderSize.Width;
        double h = AdornedElement.RenderSize.Height;
        var expanded = new Rect(0, h + StripGap - 4,
            w, StripHeight + DiamondHalf * 2 + 8);
        return expanded.Contains(p.HitPoint)
            ? new PointHitTestResult(this, p.HitPoint)
            : base.HitTestCore(p);
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = AdornedElement.RenderSize.Width;
        double h = AdornedElement.RenderSize.Height;

        _stripLeft  = 0;
        _stripWidth = w;

        var lineBrush = Application.Current?.TryFindResource("XD_GradientEditorLineBrush") as Brush
                        ?? new SolidColorBrush(Color.FromRgb(0x50, 0x50, 0x50));

        // Border around strip.
        var borderPen = new Pen(lineBrush, 1.0);
        borderPen.Freeze();

        // Gradient fill.
        var gradStops = new GradientStopCollection(
            _stops.Select(s => new GradientStop(s.Color, s.Offset)));
        var gradBrush = new LinearGradientBrush(gradStops,
            new Point(0, 0.5), new Point(1, 0.5));

        var stripRect = new Rect(_stripLeft, h + StripGap, _stripWidth, StripHeight);
        dc.DrawRectangle(gradBrush, borderPen, stripRect);

        // Checkerboard background (transparent stops preview).
        // Draw thin white/gray tiles behind the gradient, only visible for transparent stops.

        // ── Diamond handles ────────────────────────────────────────────────────

        var selectedBrush = Application.Current?.TryFindResource("XD_GradientStopSelectedBrush") as Brush
                            ?? new SolidColorBrush(Color.FromRgb(0xFF, 0xC6, 0x66));
        var normalBrush   = new SolidColorBrush(Color.FromArgb(220, 220, 220, 220));
        var outlinePen    = new Pen(Brushes.Black, 0.8);
        outlinePen.Freeze();

        for (int i = 0; i < _stops.Count; i++)
        {
            double cx  = _stripLeft + _stops[i].Offset * _stripWidth;
            double tipY = h + DiamondVPos;

            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(new Point(cx, tipY), true, true);
                ctx.LineTo(new Point(cx - DiamondHalf, tipY + DiamondHalf), true, false);
                ctx.LineTo(new Point(cx, tipY + DiamondHalf * 1.6), true, false);
                ctx.LineTo(new Point(cx + DiamondHalf, tipY + DiamondHalf), true, false);
            }
            geo.Freeze();

            var fill = i == _selectedIdx ? selectedBrush : normalBrush;
            dc.DrawGeometry(fill, outlinePen, geo);
        }
    }

    // ── Mouse interaction ─────────────────────────────────────────────────────

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        var pos   = e.GetPosition(this);
        int hit   = HitStopIndex(pos);

        if (hit >= 0)
        {
            _selectedIdx = hit;
            _draggingIdx = hit;
            CaptureMouse();
        }
        else if (IsOnStrip(pos))
        {
            double offset = Math.Clamp((pos.X - _stripLeft) / _stripWidth, 0.0, 1.0);
            var    color  = SampleGradientAt(offset);
            _stops.Add(new GradientStopInfo(color, offset));
            SortStops();
            _selectedIdx = _stops.FindIndex(s => Math.Abs(s.Offset - offset) < 0.001);
            _draggingIdx = _selectedIdx;
            CaptureMouse();
            FireChanged();
        }

        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_draggingIdx < 0 || _draggingIdx >= _stops.Count) return;

        var    pos    = e.GetPosition(this);
        double offset = Math.Clamp((pos.X - _stripLeft) / _stripWidth, 0.0, 1.0);
        var    old    = _stops[_draggingIdx];
        _stops[_draggingIdx] = new GradientStopInfo(old.Color, offset);
        SortStops();
        _draggingIdx = _stops.FindIndex(s => Math.Abs(s.Offset - offset) < 0.005);

        InvalidateVisual();
        FireChanged();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (_draggingIdx >= 0)
        {
            _draggingIdx = -1;
            ReleaseMouseCapture();
        }
    }

    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonDown(e);
        var pos = e.GetPosition(this);
        int hit = HitStopIndex(pos);
        if (hit >= 0 && _stops.Count > 2)
        {
            _stops.RemoveAt(hit);
            _selectedIdx = Math.Clamp(_selectedIdx, 0, _stops.Count - 1);
            InvalidateVisual();
            FireChanged();
            e.Handled = true;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private bool IsOnStrip(Point pos)
    {
        double h = AdornedElement.RenderSize.Height;
        var rect = new Rect(_stripLeft, h + StripGap, _stripWidth, StripHeight);
        return rect.Contains(pos);
    }

    private int HitStopIndex(Point pos)
    {
        double h   = AdornedElement.RenderSize.Height;
        double tipY = h + DiamondVPos;
        for (int i = 0; i < _stops.Count; i++)
        {
            double cx = _stripLeft + _stops[i].Offset * _stripWidth;
            double dx = pos.X - cx;
            double dy = pos.Y - (tipY + DiamondHalf * 0.8);
            if (Math.Sqrt(dx * dx + dy * dy) < DiamondHalf + 3)
                return i;
        }
        return -1;
    }

    private Color SampleGradientAt(double offset)
    {
        if (_stops.Count == 0) return Colors.Gray;
        if (_stops.Count == 1) return _stops[0].Color;

        for (int i = 0; i < _stops.Count - 1; i++)
        {
            var a = _stops[i];
            var b = _stops[i + 1];
            if (offset >= a.Offset && offset <= b.Offset)
            {
                double t = (offset - a.Offset) / (b.Offset - a.Offset + 1e-10);
                return InterpolateColor(a.Color, b.Color, t);
            }
        }
        return _stops[^1].Color;
    }

    private static Color InterpolateColor(Color a, Color b, double t)
        => Color.FromArgb(
            (byte)(a.A + (b.A - a.A) * t),
            (byte)(a.R + (b.R - a.R) * t),
            (byte)(a.G + (b.G - a.G) * t),
            (byte)(a.B + (b.B - a.B) * t));

    private void SortStops()
        => _stops.Sort((x, y) => x.Offset.CompareTo(y.Offset));

    private void FireChanged()
        => StopsChanged?.Invoke(this, _stops.AsReadOnly());
}
