// ==========================================================
// Project: WpfHexEditor.Plugins.DiagnosticTools
// File: Controls/MemoryGraphControl.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Description:
//     DrawingVisual-based ring-buffer working-set memory (MB) area graph.
//     Auto-scales the Y-axis to the observed peak so the graph is never flat.
//
// Architecture Notes:
//     Mirrors CpuGraphControl; accent color green (VS memory monitor palette).
// ==========================================================

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Media;

namespace WpfHexEditor.Plugins.DiagnosticTools.Controls;

/// <summary>
/// Area graph that plots the last N working-set memory samples (in MB).
/// </summary>
public sealed class MemoryGraphControl : FrameworkElement
{
    // -----------------------------------------------------------------------
    // Dependency Properties
    // -----------------------------------------------------------------------

    public static readonly DependencyProperty SamplesProperty =
        DependencyProperty.Register(nameof(Samples),
            typeof(ObservableCollection<double>),
            typeof(MemoryGraphControl),
            new FrameworkPropertyMetadata(null, OnSamplesChanged));

    public ObservableCollection<double>? Samples
    {
        get => (ObservableCollection<double>?)GetValue(SamplesProperty);
        set => SetValue(SamplesProperty, value);
    }

    private static void OnSamplesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctrl = (MemoryGraphControl)d;
        if (e.OldValue is INotifyCollectionChanged old)
            old.CollectionChanged -= ctrl.OnCollectionChanged;
        if (e.NewValue is INotifyCollectionChanged nw)
            nw.CollectionChanged += ctrl.OnCollectionChanged;
        ctrl.InvalidateVisual();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => InvalidateVisual();

    // -----------------------------------------------------------------------
    // Rendering
    // -----------------------------------------------------------------------

    private static readonly Brush s_lineBrush = new SolidColorBrush(Color.FromArgb(220, 100, 200, 120));
    private static readonly Brush s_fillBrush = new SolidColorBrush(Color.FromArgb(  55, 100, 200, 120));
    private static readonly Pen   s_linePen;
    private static readonly Brush s_gridBrush = new SolidColorBrush(Color.FromArgb(  30, 200, 200, 200));
    private static readonly Pen   s_gridPen;

    static MemoryGraphControl()
    {
        s_lineBrush.Freeze();
        s_fillBrush.Freeze();
        s_linePen = new Pen(s_lineBrush, 1.5); s_linePen.Freeze();
        s_gridBrush.Freeze();
        s_gridPen = new Pen(s_gridBrush, 1.0); s_gridPen.Freeze();
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth;
        double h = ActualHeight;
        if (w < 2 || h < 2) return;

        dc.DrawRectangle(
            TryFindResource("DT_GraphBackground") as Brush
                ?? new SolidColorBrush(Color.FromRgb(30, 30, 30)),
            null,
            new Rect(0, 0, w, h));

        var data = Samples;
        if (data is null || data.Count < 2)
        {
            DrawGrid(dc, w, h, 256);
            return;
        }

        // Auto-scale: peak rounded up to nearest 64 MB (minimum 64 MB).
        double peak    = data.Max();
        double yMax    = Math.Max(64, Math.Ceiling(peak / 64.0) * 64.0);

        // Grid lines at ¼, ½, ¾ of yMax.
        DrawGrid(dc, w, h, yMax);

        int    count = data.Count;
        double step  = w / (count - 1);

        var pts = new Point[count];
        for (int i = 0; i < count; i++)
            pts[i] = new Point(i * step, h - Math.Clamp(data[i] / yMax, 0, 1) * h);

        // Filled area
        var fillGeom = new StreamGeometry();
        using (var ctx = fillGeom.Open())
        {
            ctx.BeginFigure(new Point(0, h), isFilled: true, isClosed: true);
            foreach (var pt in pts) ctx.LineTo(pt, isStroked: false, isSmoothJoin: false);
            ctx.LineTo(new Point(pts[^1].X, h), isStroked: false, isSmoothJoin: false);
        }
        fillGeom.Freeze();
        dc.DrawGeometry(s_fillBrush, null, fillGeom);

        for (int i = 0; i < pts.Length - 1; i++)
            dc.DrawLine(s_linePen, pts[i], pts[i + 1]);
    }

    private static void DrawGrid(DrawingContext dc, double w, double h, double yMax)
    {
        foreach (double frac in new[] { 0.25, 0.5, 0.75 })
        {
            double y = h - frac * h;
            dc.DrawLine(s_gridPen, new Point(0, y), new Point(w, y));
        }
    }
}
