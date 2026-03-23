// ==========================================================
// Project: WpfHexEditor.Plugins.DiagnosticTools
// File: Controls/GraphControlBase.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Description:
//     Abstract base class for DrawingVisual ring-buffer area graphs.
//     Provides common DP (Samples), OnRender skeleton (background, grid,
//     area fill, polyline, overlay labels) and theme-resource hooks.
//     Subclasses override Y-axis scaling and theme token keys only.
//
// Architecture Notes:
//     Pattern: Template Method — OnRender is the invariant algorithm;
//     GetYMax / GetLineColorKey / GetFillColorKey are the variant hooks.
//     Brushes and Pens are created per-render (not static readonly) so
//     theme changes take effect immediately on the next paint.
// ==========================================================

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace WpfHexEditor.Plugins.DiagnosticTools.Controls;

/// <summary>
/// Base class for CPU and Memory area graphs.
/// </summary>
public abstract class GraphControlBase : FrameworkElement
{
    // -----------------------------------------------------------------------
    // Dependency Property
    // -----------------------------------------------------------------------

    public static readonly DependencyProperty SamplesProperty =
        DependencyProperty.Register(
            nameof(Samples),
            typeof(ObservableCollection<double>),
            typeof(GraphControlBase),
            new FrameworkPropertyMetadata(null, OnSamplesChanged));

    public ObservableCollection<double>? Samples
    {
        get => (ObservableCollection<double>?)GetValue(SamplesProperty);
        set => SetValue(SamplesProperty, value);
    }

    private static void OnSamplesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctrl = (GraphControlBase)d;
        if (e.OldValue is INotifyCollectionChanged old)
            old.CollectionChanged -= ctrl.OnCollectionChanged;
        if (e.NewValue is INotifyCollectionChanged nw)
            nw.CollectionChanged += ctrl.OnCollectionChanged;
        ctrl.InvalidateVisual();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => InvalidateVisual();

    // -----------------------------------------------------------------------
    // Abstract hooks
    // -----------------------------------------------------------------------

    /// <summary>Returns the Y-axis maximum for the current data set.</summary>
    protected abstract double GetYMax(ObservableCollection<double> data);

    /// <summary>Resource key for the line stroke brush (e.g. "DT_CpuLineColor").</summary>
    protected abstract string LineColorKey { get; }

    /// <summary>Resource key for the area fill brush (e.g. "DT_CpuFillColor").</summary>
    protected abstract string FillColorKey { get; }

    /// <summary>Unit suffix appended to Y-axis overlay labels (e.g. "%" or " MB").</summary>
    protected abstract string UnitSuffix { get; }

    // -----------------------------------------------------------------------
    // OnRender — Template Method
    // -----------------------------------------------------------------------

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth;
        double h = ActualHeight;
        if (w < 2 || h < 2) return;

        // -- Background
        var bgBrush = TryFindResource("DT_GraphBackground") as Brush
            ?? new SolidColorBrush(Color.FromRgb(37, 37, 38));
        dc.DrawRectangle(bgBrush, null, new Rect(0, 0, w, h));

        var data = Samples;
        double yMax = (data is { Count: > 0 }) ? GetYMax(data) : GetDefaultYMax();

        // -- Grid lines (¼ ½ ¾)
        var gridBrush = TryFindResource("DT_GraphGridLine") as Brush
            ?? new SolidColorBrush(Color.FromArgb(40, 200, 200, 200));
        var gridPen = new Pen(gridBrush, 1.0);
        foreach (double frac in new[] { 0.25, 0.5, 0.75 })
        {
            double gy = h - frac * h;
            dc.DrawLine(gridPen, new Point(0, gy), new Point(w, gy));
        }

        if (data is null || data.Count < 2)
        {
            DrawOverlayLabel(dc, w, h, "—", yMax);
            return;
        }

        int count = data.Count;
        double step = w / (count - 1);

        // Build point array
        var pts = new Point[count];
        for (int i = 0; i < count; i++)
            pts[i] = new Point(i * step, h - Math.Clamp(data[i] / yMax, 0, 1) * h);

        // -- Filled area
        var fillBrush = TryFindResource(FillColorKey) as Brush
            ?? new SolidColorBrush(Color.FromArgb(55, 100, 150, 200));

        var fillGeom = new StreamGeometry();
        using (var ctx = fillGeom.Open())
        {
            ctx.BeginFigure(new Point(0, h), isFilled: true, isClosed: true);
            foreach (var pt in pts) ctx.LineTo(pt, isStroked: false, isSmoothJoin: false);
            ctx.LineTo(new Point(pts[^1].X, h), isStroked: false, isSmoothJoin: false);
        }
        fillGeom.Freeze();
        dc.DrawGeometry(fillBrush, null, fillGeom);

        // -- Polyline
        var lineBrush = TryFindResource(LineColorKey) as Brush
            ?? new SolidColorBrush(Color.FromArgb(220, 86, 156, 214));
        var linePen = new Pen(lineBrush, 1.5);
        for (int i = 0; i < pts.Length - 1; i++)
            dc.DrawLine(linePen, pts[i], pts[i + 1]);

        // -- Overlay: current value bottom-right
        DrawOverlayLabel(dc, w, h, $"{data[^1]:F1}{UnitSuffix}", yMax);
    }

    // -----------------------------------------------------------------------

    /// <summary>
    /// Draws a small text label in the bottom-right corner of the graph
    /// showing the most-recent value (and optionally the Y-max scale).
    /// </summary>
    private void DrawOverlayLabel(DrawingContext dc, double w, double h, string text, double yMax)
    {
        var labelBrush = TryFindResource("DT_GraphLabelFg") as Brush ?? Brushes.DarkGray;

        var ft = new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Consolas"),
            10,
            labelBrush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        dc.DrawText(ft, new Point(w - ft.Width - 4, h - ft.Height - 2));

        // Y-axis max label top-right
        var maxText = new FormattedText(
            $"{yMax:F0}{UnitSuffix}",
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Consolas"),
            9,
            labelBrush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        dc.DrawText(maxText, new Point(w - maxText.Width - 4, 2));
    }

    /// <summary>Default Y-max when no data is available (subclass override optional).</summary>
    protected virtual double GetDefaultYMax() => 100.0;
}
