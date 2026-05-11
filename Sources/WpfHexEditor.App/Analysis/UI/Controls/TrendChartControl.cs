// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/UI/Controls/TrendChartControl.cs
// Description:
//     Multi-series line chart for historical code-analysis snapshots.
//     Draws up to N series sharing the same X (snapshot index) with
//     per-series stroke color, legend, and a baseline grid. Allocation-
//     free OnRender; geometry rebuilt only on data/size change.
// Architecture Notes:
//     - Inherits FrameworkElement — no template, no input.
//     - Series binding via TrendChartSeries[] DP; each series has Label,
//       Values, Brush, and optional Unit.
// ==========================================================

using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace WpfHexEditor.App.Analysis.UI.Controls;

/// <summary>One trend line in the chart.</summary>
public sealed class TrendChartSeries
{
    public string         Label  { get; init; } = "";
    public IList<double>  Values { get; init; } = [];
    public Brush          Stroke { get; init; } = Brushes.SteelBlue;
    public string?        Unit   { get; init; }
}

public sealed class TrendChartControl : FrameworkElement
{
    public static readonly DependencyProperty SeriesProperty =
        DependencyProperty.Register(nameof(Series), typeof(IList<TrendChartSeries>), typeof(TrendChartControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public IList<TrendChartSeries>? Series
    {
        get => (IList<TrendChartSeries>?)GetValue(SeriesProperty);
        set => SetValue(SeriesProperty, value);
    }

    private static readonly Pen GridPen = new(new SolidColorBrush(Color.FromArgb(40, 128, 128, 128)), 0.5);
    private static readonly Typeface LegendFace = new("Segoe UI");

    static TrendChartControl()
    {
        GridPen.Freeze();
    }

    protected override void OnRender(DrawingContext dc)
    {
        var b = new Rect(0, 0, ActualWidth, ActualHeight);
        if (b.Width < 20 || b.Height < 20) return;
        if (Series is null || Series.Count == 0) return;

        const double padding = 18;
        const double legendHeight = 18;
        var plot = new Rect(padding, padding,
                            Math.Max(0, b.Width - padding * 2),
                            Math.Max(0, b.Height - padding * 2 - legendHeight));
        if (plot.Width < 1 || plot.Height < 1) return;

        // Compute global min/max across all series with values.
        double min = double.MaxValue, max = double.MinValue;
        int maxCount = 0;
        foreach (var s in Series)
        {
            if (s?.Values is null) continue;
            foreach (var v in s.Values) { if (v < min) min = v; if (v > max) max = v; }
            if (s.Values.Count > maxCount) maxCount = s.Values.Count;
        }
        if (maxCount < 2 || max <= min) return;

        // Horizontal baseline grid (4 lines).
        for (int i = 0; i <= 4; i++)
        {
            var y = plot.Y + plot.Height * i / 4.0;
            dc.DrawLine(GridPen, new Point(plot.X, y), new Point(plot.Right, y));
        }

        // Draw series.
        foreach (var s in Series)
        {
            if (s?.Values is null || s.Values.Count < 2) continue;
            var pen = new Pen(s.Stroke, 1.5);
            pen.Freeze();
            var geom = new StreamGeometry();
            using (var ctx = geom.Open())
            {
                for (int i = 0; i < s.Values.Count; i++)
                {
                    var x = plot.X + plot.Width * i / (s.Values.Count - 1.0);
                    var y = plot.Bottom - plot.Height * (s.Values[i] - min) / (max - min);
                    if (i == 0) ctx.BeginFigure(new Point(x, y), false, false);
                    else        ctx.LineTo(new Point(x, y), true, false);
                }
            }
            geom.Freeze();
            dc.DrawGeometry(null, pen, geom);
        }

        // Legend along the bottom.
        double lx = plot.X;
        var legendY = plot.Bottom + 4;
        foreach (var s in Series)
        {
            if (s is null) continue;
            var swatch = new Rect(lx, legendY + 4, 10, 10);
            dc.DrawRectangle(s.Stroke, null, swatch);
            var ft = new FormattedText(s.Label ?? "?",
                CultureInfo.InvariantCulture, FlowDirection.LeftToRight, LegendFace, 11,
                Brushes.Gray, 1.0);
            dc.DrawText(ft, new Point(lx + 14, legendY));
            lx += 14 + ft.Width + 12;
            if (lx > plot.Right - 60) break; // overflow guard
        }
    }
}
