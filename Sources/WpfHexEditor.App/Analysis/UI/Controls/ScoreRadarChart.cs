// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/UI/Controls/ScoreRadarChart.cs
// Description: Lightweight 6-axis radar chart drawn on a Canvas-derived
//              control. Six values 0..100: Volume, Complexity, Coupling,
//              Duplication, DeadCode, Conventions. No third-party deps.
// Architecture Notes:
//     OnRender override — the only WPF API used is DrawingContext primitives.
// ==========================================================

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WpfHexEditor.App.Analysis.UI.Controls;

public sealed class ScoreRadarChart : Control
{
    public static readonly DependencyProperty VolumeProperty       = Reg(nameof(Volume));
    public static readonly DependencyProperty ComplexityProperty   = Reg(nameof(Complexity));
    public static readonly DependencyProperty CouplingProperty     = Reg(nameof(Coupling));
    public static readonly DependencyProperty DuplicationProperty  = Reg(nameof(Duplication));
    public static readonly DependencyProperty DeadCodeProperty     = Reg(nameof(DeadCode));
    public static readonly DependencyProperty ConventionsProperty  = Reg(nameof(Conventions));

    public int Volume      { get => (int)GetValue(VolumeProperty);      set => SetValue(VolumeProperty,      value); }
    public int Complexity  { get => (int)GetValue(ComplexityProperty);  set => SetValue(ComplexityProperty,  value); }
    public int Coupling    { get => (int)GetValue(CouplingProperty);    set => SetValue(CouplingProperty,    value); }
    public int Duplication { get => (int)GetValue(DuplicationProperty); set => SetValue(DuplicationProperty, value); }
    public int DeadCode    { get => (int)GetValue(DeadCodeProperty);    set => SetValue(DeadCodeProperty,    value); }
    public int Conventions { get => (int)GetValue(ConventionsProperty); set => SetValue(ConventionsProperty, value); }

    private static DependencyProperty Reg(string name) =>
        DependencyProperty.Register(name, typeof(int), typeof(ScoreRadarChart),
            new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));

    static ScoreRadarChart()
    {
        // Force re-render when the inherited Foreground brush changes (theme switch).
        ForegroundProperty.OverrideMetadata(typeof(ScoreRadarChart),
            new FrameworkPropertyMetadata(SystemColors.ControlTextBrush,
                FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.Inherits));
    }

    private static readonly string[] Labels = ["Vol", "CC", "Coup", "Dup", "Dead", "Conv"];

    protected override void OnRender(DrawingContext dc)
    {
        var size   = new Size(ActualWidth, ActualHeight);
        var center = new Point(size.Width / 2, size.Height / 2);
        double r   = Math.Min(size.Width, size.Height) / 2 - 18;
        if (r <= 0) return;

        var grid    = new Pen(new SolidColorBrush(Color.FromArgb(80, 128, 128, 128)), 1);
        var axis    = new Pen(new SolidColorBrush(Color.FromArgb(120, 128, 128, 128)), 1);
        var fill    = new SolidColorBrush(Color.FromArgb(110, 0, 122, 204));
        var stroke  = new Pen(new SolidColorBrush(Color.FromArgb(255, 0, 122, 204)), 2);
        // Use the inherited Foreground for labels. Any Brush subtype works for DrawText,
        // not just SolidColorBrush — never silently downgrade to gray on theme aliases.
        var labelBr = Foreground ?? SystemColors.ControlTextBrush;

        // Concentric grid rings
        for (int ring = 1; ring <= 4; ring++)
        {
            double rr = r * ring / 4.0;
            var poly = BuildPolygon(center, rr, [100,100,100,100,100,100]);
            dc.DrawGeometry(null, grid, poly);
        }

        // Axes
        for (int i = 0; i < 6; i++)
        {
            double a = AxisAngle(i);
            var p = new Point(center.X + r * Math.Cos(a), center.Y + r * Math.Sin(a));
            dc.DrawLine(axis, center, p);

            var labelP = new Point(center.X + (r + 10) * Math.Cos(a), center.Y + (r + 10) * Math.Sin(a));
            var ft = new FormattedText(Labels[i], CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), 10, labelBr, 1.0);
            dc.DrawText(ft, new Point(labelP.X - ft.Width / 2, labelP.Y - ft.Height / 2));
        }

        // Score polygon
        int[] vals = [Volume, Complexity, Coupling, Duplication, DeadCode, Conventions];
        var scorePoly = BuildPolygon(center, r, vals);
        dc.DrawGeometry(fill, stroke, scorePoly);
    }

    private static double AxisAngle(int i) => -Math.PI / 2 + i * Math.PI * 2 / 6;

    private static Geometry BuildPolygon(Point center, double radius, int[] values)
    {
        var fig = new PathFigure { IsClosed = true, IsFilled = true };
        for (int i = 0; i < 6; i++)
        {
            double v = Math.Clamp(values[i], 0, 100) / 100.0;
            double a = AxisAngle(i);
            var p = new Point(center.X + radius * v * Math.Cos(a), center.Y + radius * v * Math.Sin(a));
            if (i == 0) fig.StartPoint = p;
            else        fig.Segments.Add(new LineSegment(p, true));
        }
        return new PathGeometry { Figures = { fig } };
    }
}
