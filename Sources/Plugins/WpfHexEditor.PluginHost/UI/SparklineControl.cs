// ==========================================================
// Project: WpfHexEditor.PluginHost.UI
// File: SparklineControl.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-07
// Description:
//     Lightweight WPF FrameworkElement that renders a sparkline
//     chart via OnRender. Auto-updates when the bound ChartPoint
//     collection changes (subscribes to INotifyCollectionChanged).
//     Used in DataGrid mini-chart columns and the detail pane.
//
// Architecture Notes:
//     No external library. Pure WPF DrawingContext rendering.
//     DependencyProperty PointsSource drives re-render on collection change.
// ==========================================================

using System.Collections.Specialized;
using System.Windows;
using System.Windows.Media;
using WpfHexEditor.PluginHost.UI;

namespace WpfHexEditor.PluginHost.UI;

/// <summary>
/// Renders a sparkline line chart from a <see cref="IReadOnlyList{ChartPoint}"/> source.
/// Automatically redraws when the source collection notifies of changes.
/// </summary>
public sealed class SparklineControl : FrameworkElement
{
    // -- Dependency Properties ---------------------------------------------------

    public static readonly DependencyProperty PointsSourceProperty =
        DependencyProperty.Register(
            nameof(PointsSource),
            typeof(IReadOnlyList<ChartPoint>),
            typeof(SparklineControl),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnPointsSourceChanged));

    public static readonly DependencyProperty LineColorProperty =
        DependencyProperty.Register(
            nameof(LineColor),
            typeof(Color),
            typeof(SparklineControl),
            new FrameworkPropertyMetadata(Colors.LimeGreen, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MaxValueProperty =
        DependencyProperty.Register(
            nameof(MaxValue),
            typeof(double),
            typeof(SparklineControl),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeThicknessProperty =
        DependencyProperty.Register(
            nameof(StrokeThickness),
            typeof(double),
            typeof(SparklineControl),
            new FrameworkPropertyMetadata(1.5, FrameworkPropertyMetadataOptions.AffectsRender));

    // -- CLR wrappers ------------------------------------------------------------

    /// <summary>Collection of data points to render. Subscribes to CollectionChanged automatically.</summary>
    public IReadOnlyList<ChartPoint>? PointsSource
    {
        get => (IReadOnlyList<ChartPoint>?)GetValue(PointsSourceProperty);
        set => SetValue(PointsSourceProperty, value);
    }

    /// <summary>Stroke color of the sparkline.</summary>
    public Color LineColor
    {
        get => (Color)GetValue(LineColorProperty);
        set => SetValue(LineColorProperty, value);
    }

    /// <summary>
    /// Fixed Y-axis maximum value (e.g. 100 for CPU%).
    /// Use 0 (default) for auto-scaling to the observed peak.
    /// </summary>
    public double MaxValue
    {
        get => (double)GetValue(MaxValueProperty);
        set => SetValue(MaxValueProperty, value);
    }

    /// <summary>Thickness of the sparkline stroke.</summary>
    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    // -- Collection subscription -------------------------------------------------

    private INotifyCollectionChanged? _subscribedCollection;

    private static void OnPointsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctrl = (SparklineControl)d;

        if (ctrl._subscribedCollection is not null)
        {
            ctrl._subscribedCollection.CollectionChanged -= ctrl.OnCollectionChanged;
            ctrl._subscribedCollection = null;
        }

        if (e.NewValue is INotifyCollectionChanged col)
        {
            col.CollectionChanged += ctrl.OnCollectionChanged;
            ctrl._subscribedCollection = col;
        }
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => InvalidateVisual();

    // -- Rendering ---------------------------------------------------------------

    protected override void OnRender(DrawingContext dc)
    {
        var points = PointsSource;
        if (points is null || points.Count < 2) return;

        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        var peak = MaxValue > 0 ? MaxValue : points.Max(p => p.Value);
        if (peak <= 0) peak = 1; // avoid divide-by-zero

        var count  = points.Count;
        var pen    = new Pen(new SolidColorBrush(LineColor), StrokeThickness);
        pen.Freeze();

        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(MapPoint(0, points[0].Value, w, h, peak, count), false, false);
            for (var i = 1; i < count; i++)
                ctx.LineTo(MapPoint(i, points[i].Value, w, h, peak, count), true, false);
        }
        geo.Freeze();

        dc.DrawGeometry(null, pen, geo);
    }

    private static Point MapPoint(int index, double value, double w, double h, double peak, int count)
        => new(w * index / (count - 1), h - h * value / peak);
}
