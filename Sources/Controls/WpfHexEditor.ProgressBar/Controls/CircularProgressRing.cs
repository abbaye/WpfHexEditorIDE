// ==========================================================
// Project: WpfHexEditor.ProgressBar
// File: Controls/CircularProgressRing.cs
// Description:
//     Circular arc progress ring. Supports determinate fill arc,
//     indeterminate spinning arc, and percentage text overlay.
//     Pure OnRender — no Path/ArcSegment/Storyboard overhead.
// ==========================================================

using System.Windows;
using System.Windows.Media;

namespace WpfHexEditor.ProgressBar.Controls;

/// <summary>
/// Lightweight circular progress ring rendered via <see cref="OnRender"/>.
/// </summary>
public sealed class CircularProgressRing : ProgressBarBase
{
    // ── Additional DPs ────────────────────────────────────────────────────────

    public static readonly DependencyProperty StrokeThicknessProperty =
        DependencyProperty.Register(nameof(StrokeThickness), typeof(double), typeof(CircularProgressRing),
            new FrameworkPropertyMetadata(2.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StartAngleProperty =
        DependencyProperty.Register(nameof(StartAngle), typeof(double), typeof(CircularProgressRing),
            new FrameworkPropertyMetadata(-90.0, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Stroke width of the ring. Defaults to 2px.</summary>
    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    /// <summary>Start angle in degrees. -90 = top (12 o'clock).</summary>
    public double StartAngle
    {
        get => (double)GetValue(StartAngleProperty);
        set => SetValue(StartAngleProperty, value);
    }

    // ── Render ────────────────────────────────────────────────────────────────

    protected override void OnRender(DrawingContext dc)
    {
        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        var stroke = StrokeThickness;
        var radius = Math.Min(w, h) / 2.0 - stroke / 2.0;
        if (radius <= 0) return;

        var center = new Point(w / 2.0, h / 2.0);

        var trackPen = new Pen(ResolveTrackBrush(), stroke) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        var fillPen  = new Pen(ResolveActiveBrush(), stroke) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        if (trackPen.CanFreeze) trackPen.Freeze();
        if (fillPen.CanFreeze)  fillPen.Freeze();

        // Track circle
        dc.DrawEllipse(null, trackPen, center, radius, radius);

        // Arc
        double sweepDeg;
        double startDeg;

        if (IsIndeterminate)
        {
            sweepDeg = 90;
            startDeg = StartAngle + IndeterminateOffset * 360;
        }
        else
        {
            sweepDeg = Math.Clamp(AnimatedProgress * 360, 0, 359.99);
            startDeg = StartAngle;
            if (sweepDeg < 0.5) return; // too small to render
        }

        DrawArc(dc, center, radius, startDeg, sweepDeg, fillPen);

        // Percentage text
        if (ShowPercentage && !IsIndeterminate)
        {
            var fontSize = Math.Max(6, radius * 0.7);
            var ft = CreatePercentageText(fontSize);
            dc.DrawText(ft, new Point(center.X - ft.Width / 2, center.Y - ft.Height / 2));
        }
    }

    private static void DrawArc(DrawingContext dc, Point center, double radius,
        double startDeg, double sweepDeg, Pen pen)
    {
        var startRad = startDeg * Math.PI / 180;
        var endRad   = (startDeg + sweepDeg) * Math.PI / 180;

        var startPt = new Point(
            center.X + radius * Math.Cos(startRad),
            center.Y + radius * Math.Sin(startRad));

        var endPt = new Point(
            center.X + radius * Math.Cos(endRad),
            center.Y + radius * Math.Sin(endRad));

        var figure = new PathFigure
        {
            StartPoint = startPt,
            IsClosed   = false,
            Segments =
            {
                new ArcSegment(endPt, new Size(radius, radius), 0,
                    sweepDeg > 180, SweepDirection.Clockwise, true)
            }
        };

        var geo = new PathGeometry { Figures = { figure } };
        if (geo.CanFreeze) geo.Freeze();

        dc.DrawGeometry(null, pen, geo);
    }
}
