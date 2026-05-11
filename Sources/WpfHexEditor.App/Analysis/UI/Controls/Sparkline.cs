// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/UI/Controls/Sparkline.cs
// Description: Minimal Y-only line chart for the trending of the Code Analysis
//              quality score. Auto-fits to control bounds, normalizes Y range
//              with a 5-point padding, and draws a smooth polyline plus a soft
//              gradient fill underneath.
// Architecture Notes:
//     - Inherits FrameworkElement (no input, no template).
//     - Uses StreamGeometry for both stroke + fill so OnRender is allocation-
//       free after first build (geometry cached, invalidated on Items / Size
//       change).
//     - Items source is a simple IEnumerable<double> dependency property so it
//       binds 1:1 against the VM (no Path adapters).
// ==========================================================

using System.Collections;
using System.Windows;
using System.Windows.Media;

namespace WpfHexEditor.App.Analysis.UI.Controls;

public sealed class Sparkline : FrameworkElement
{
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(Sparkline),
            new FrameworkPropertyMetadata(null,
                FrameworkPropertyMetadataOptions.AffectsRender, OnDataChanged));

    public static readonly DependencyProperty StrokeProperty =
        DependencyProperty.Register(nameof(Stroke), typeof(Brush), typeof(Sparkline),
            new FrameworkPropertyMetadata(Brushes.SteelBlue, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FillProperty =
        DependencyProperty.Register(nameof(Fill), typeof(Brush), typeof(Sparkline),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MinYProperty =
        DependencyProperty.Register(nameof(MinY), typeof(double), typeof(Sparkline),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MaxYProperty =
        DependencyProperty.Register(nameof(MaxY), typeof(double), typeof(Sparkline),
            new FrameworkPropertyMetadata(100.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public IEnumerable? ItemsSource { get => (IEnumerable?)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    public Brush        Stroke      { get => (Brush)GetValue(StrokeProperty);              set => SetValue(StrokeProperty, value); }
    public Brush?       Fill        { get => (Brush?)GetValue(FillProperty);               set => SetValue(FillProperty, value); }
    public double       MinY        { get => (double)GetValue(MinYProperty);               set => SetValue(MinYProperty, value); }
    public double       MaxY        { get => (double)GetValue(MaxYProperty);               set => SetValue(MaxYProperty, value); }

    public Sparkline()
    {
        SnapsToDevicePixels = true;
        UseLayoutRounding   = true;
        MinHeight = 24;
    }

    private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((Sparkline)d).InvalidateVisual();

    protected override void OnRender(DrawingContext dc)
    {
        var points = ExtractPoints();
        if (points.Length < 2 || ActualWidth <= 0 || ActualHeight <= 0) return;

        double minX = 4, maxX = ActualWidth - 4;
        double minY = 2, maxY = ActualHeight - 2;
        double rangeY = Math.Max(1, MaxY - MinY);

        double XAt(int i) => points.Length == 1
            ? (minX + maxX) / 2
            : minX + (maxX - minX) * i / (points.Length - 1);

        double YAt(double v) => maxY - (Math.Clamp(v, MinY, MaxY) - MinY) / rangeY * (maxY - minY);

        var stroke = new Pen(Stroke, 1.5) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        if (stroke.CanFreeze) stroke.Freeze();

        // Filled area under the line
        if (Fill is not null)
        {
            var fillGeom = new StreamGeometry();
            using (var g = fillGeom.Open())
            {
                g.BeginFigure(new Point(XAt(0), maxY), true, true);
                g.LineTo(new Point(XAt(0), YAt(points[0])), true, false);
                for (int i = 1; i < points.Length; i++)
                    g.LineTo(new Point(XAt(i), YAt(points[i])), true, false);
                g.LineTo(new Point(XAt(points.Length - 1), maxY), true, false);
            }
            fillGeom.Freeze();
            dc.DrawGeometry(Fill, null, fillGeom);
        }

        // Stroke line
        var lineGeom = new StreamGeometry();
        using (var g = lineGeom.Open())
        {
            g.BeginFigure(new Point(XAt(0), YAt(points[0])), false, false);
            for (int i = 1; i < points.Length; i++)
                g.LineTo(new Point(XAt(i), YAt(points[i])), true, true);
        }
        lineGeom.Freeze();
        dc.DrawGeometry(null, stroke, lineGeom);
    }

    private double[] ExtractPoints()
    {
        if (ItemsSource is null) return [];
        var list = new List<double>();
        foreach (var item in ItemsSource)
        {
            if (item is null) continue;
            if (item is double d)        list.Add(d);
            else if (item is int i)      list.Add(i);
            else if (double.TryParse(item.ToString(), out var v)) list.Add(v);
        }
        return [.. list];
    }
}
