// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: MeasureGuideAdorner.cs
// Description:
//     Adorner that draws distance lines from the hovered element to its nearest
//     sibling edges (left/right/top/bottom) when the user holds Alt on the canvas.
//     Mimics the Chrome DevTools / Sketch distance measurement experience.
//
// Architecture Notes:
//     Adorner on DesignRoot. Non-hit-testable, purely decorative.
//     Updated via Refresh(element, siblings). Cleared via Clear().
// ==========================================================

using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace WpfHexEditor.Editor.XamlDesigner.Controls;

/// <summary>
/// Shows Alt+hover distance lines from the measured element to the nearest sibling edges.
/// </summary>
public sealed class MeasureGuideAdorner : Adorner
{
    private readonly record struct MeasureLine(
        Point Start,
        Point End,
        string Label,
        bool IsHorizontal);

    private readonly List<MeasureLine> _lines = new();

    public MeasureGuideAdorner(UIElement adornedElement) : base(adornedElement)
    {
        IsHitTestVisible = false;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Recomputes distance lines from <paramref name="target"/> to the nearest elements
    /// in <paramref name="siblings"/> (all in DesignRoot / AdornedElement coordinate space).
    /// </summary>
    public void Refresh(FrameworkElement target, IEnumerable<FrameworkElement> siblings)
    {
        _lines.Clear();

        var tp = target.TranslatePoint(new Point(0, 0), AdornedElement);
        var tRect = new Rect(tp.X, tp.Y, target.ActualWidth, target.ActualHeight);

        double? nearLeft   = null;
        double? nearRight  = null;
        double? nearTop    = null;
        double? nearBottom = null;

        foreach (var sib in siblings)
        {
            if (ReferenceEquals(sib, target)) continue;

            var sp   = sib.TranslatePoint(new Point(0, 0), AdornedElement);
            var sRect = new Rect(sp.X, sp.Y, sib.ActualWidth, sib.ActualHeight);

            // Check if sibling overlaps vertically (for left/right gaps).
            bool overlapsV = tRect.Top < sRect.Bottom && tRect.Bottom > sRect.Top;
            // Check if sibling overlaps horizontally (for top/bottom gaps).
            bool overlapsH = tRect.Left < sRect.Right && tRect.Right > sRect.Left;

            if (overlapsV)
            {
                // Left gap: sibling is to the left
                if (sRect.Right <= tRect.Left)
                {
                    double gap = tRect.Left - sRect.Right;
                    if (nearLeft is null || gap < nearLeft)
                        nearLeft = gap;
                }
                // Right gap: sibling is to the right
                if (sRect.Left >= tRect.Right)
                {
                    double gap = sRect.Left - tRect.Right;
                    if (nearRight is null || gap < nearRight)
                        nearRight = gap;
                }
            }

            if (overlapsH)
            {
                // Top gap
                if (sRect.Bottom <= tRect.Top)
                {
                    double gap = tRect.Top - sRect.Bottom;
                    if (nearTop is null || gap < nearTop)
                        nearTop = gap;
                }
                // Bottom gap
                if (sRect.Top >= tRect.Bottom)
                {
                    double gap = sRect.Top - tRect.Bottom;
                    if (nearBottom is null || gap < nearBottom)
                        nearBottom = gap;
                }
            }
        }

        double midY = tRect.Top + tRect.Height / 2.0;
        double midX = tRect.Left + tRect.Width / 2.0;

        if (nearLeft is double gl)
            _lines.Add(new MeasureLine(
                new Point(tRect.Left - gl, midY),
                new Point(tRect.Left,      midY),
                $"{Math.Round(gl)}px", IsHorizontal: true));

        if (nearRight is double gr)
            _lines.Add(new MeasureLine(
                new Point(tRect.Right,      midY),
                new Point(tRect.Right + gr, midY),
                $"{Math.Round(gr)}px", IsHorizontal: true));

        if (nearTop is double gt)
            _lines.Add(new MeasureLine(
                new Point(midX, tRect.Top - gt),
                new Point(midX, tRect.Top),
                $"{Math.Round(gt)}px", IsHorizontal: false));

        if (nearBottom is double gb)
            _lines.Add(new MeasureLine(
                new Point(midX, tRect.Bottom),
                new Point(midX, tRect.Bottom + gb),
                $"{Math.Round(gb)}px", IsHorizontal: false));

        InvalidateVisual();
    }

    /// <summary>Clears all measure lines.</summary>
    public void Clear()
    {
        _lines.Clear();
        InvalidateVisual();
    }

    // ── Rendering ─────────────────────────────────────────────────────────────

    protected override void OnRender(DrawingContext dc)
    {
        if (_lines.Count == 0) return;

        var lineBrush = Application.Current?.TryFindResource("XD_MeasureGuideBrush") as Brush
                        ?? new SolidColorBrush(Color.FromRgb(0xFF, 0x8C, 0x00));
        var labelBg   = Application.Current?.TryFindResource("XD_MeasureLabelBackground") as Brush
                        ?? new SolidColorBrush(Color.FromArgb(200, 30, 30, 30));

        var pen = new Pen(lineBrush, 1.0);
        pen.Freeze();

        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        foreach (var line in _lines)
        {
            dc.DrawLine(pen, line.Start, line.End);

            // End-caps (short perpendicular ticks).
            const double tick = 4;
            if (line.IsHorizontal)
            {
                dc.DrawLine(pen, new Point(line.Start.X, line.Start.Y - tick), new Point(line.Start.X, line.Start.Y + tick));
                dc.DrawLine(pen, new Point(line.End.X,   line.End.Y   - tick), new Point(line.End.X,   line.End.Y   + tick));
            }
            else
            {
                dc.DrawLine(pen, new Point(line.Start.X - tick, line.Start.Y), new Point(line.Start.X + tick, line.Start.Y));
                dc.DrawLine(pen, new Point(line.End.X   - tick, line.End.Y),   new Point(line.End.X   + tick, line.End.Y));
            }

            // Label badge at midpoint.
            var ft = new FormattedText(
                line.Label,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                9.0,
                lineBrush,
                dpi);

            const double padH = 3, padV = 1;
            double lw = ft.Width  + padH * 2;
            double lh = ft.Height + padV * 2;
            double lx = (line.Start.X + line.End.X) / 2.0 - lw / 2.0;
            double ly = (line.Start.Y + line.End.Y) / 2.0 - lh / 2.0;

            dc.DrawRoundedRectangle(labelBg, null, new Rect(lx, ly, lw, lh), 2, 2);
            dc.DrawText(ft, new Point(lx + padH, ly + padV));
        }
    }
}
