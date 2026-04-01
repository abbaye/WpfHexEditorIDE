// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: DesignGridOverlay.cs
// Author: Derek Tremblay
// Created: 2026-03-18
// Description:
//     Semi-transparent grid overlay drawn on top of the design canvas.
//     Renders fine dotted lines at every GridSize device-independent pixels,
//     accounting for the current zoom level and canvas offset so the grid
//     appears anchored to the design coordinate system.
//
// Architecture Notes:
//     FrameworkElement — rendered entirely via OnRender. Placed in the
//     AdornerDecorator host (same visual layer as the design canvas) via
//     a transparent hit-test overlay Grid inside _designPaneGrid so that
//     it does not interfere with adorner z-ordering.
//     Visibility toggled by XamlDesignerSplitHost.ToggleGrid().
// ==========================================================

using System.Windows;
using System.Windows.Media;

namespace WpfHexEditor.Editor.XamlDesigner.Controls;

/// <summary>
/// Transparent dotted-grid overlay drawn over the ZoomPanCanvas viewport.
/// The grid is anchored to the design canvas coordinate system so that it
/// moves and scales correctly as the user zooms and pans.
/// </summary>
public sealed class DesignGridOverlay : FrameworkElement
{
    // ── Dependency Properties ─────────────────────────────────────────────────

    public static readonly DependencyProperty GridSizeProperty =
        DependencyProperty.Register(nameof(GridSize), typeof(int), typeof(DesignGridOverlay),
            new FrameworkPropertyMetadata(8, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ZoomFactorProperty =
        DependencyProperty.Register(nameof(ZoomFactor), typeof(double), typeof(DesignGridOverlay),
            new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty OffsetXProperty =
        DependencyProperty.Register(nameof(OffsetX), typeof(double), typeof(DesignGridOverlay),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty OffsetYProperty =
        DependencyProperty.Register(nameof(OffsetY), typeof(double), typeof(DesignGridOverlay),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    // ── Properties ────────────────────────────────────────────────────────────

    /// <summary>Grid cell size in device-independent pixels. Default: 8.</summary>
    public int    GridSize   { get => (int)GetValue(GridSizeProperty);    set => SetValue(GridSizeProperty,   value); }
    public double ZoomFactor { get => (double)GetValue(ZoomFactorProperty); set => SetValue(ZoomFactorProperty, value); }
    public double OffsetX    { get => (double)GetValue(OffsetXProperty);    set => SetValue(OffsetXProperty,    value); }
    public double OffsetY    { get => (double)GetValue(OffsetYProperty);    set => SetValue(OffsetYProperty,    value); }

    // ── Constructor ───────────────────────────────────────────────────────────

    public DesignGridOverlay()
    {
        IsHitTestVisible = false;
        Visibility = Visibility.Collapsed;
    }

    // ── Rendering ─────────────────────────────────────────────────────────────

    protected override void OnRender(DrawingContext dc)
    {
        double zoom = Math.Max(0.01, ZoomFactor);
        int    gs   = Math.Max(1, GridSize);

        double cellPx = gs * zoom;

        // Hide dots when they would be so dense they become a solid fill.
        if (cellPx < 3.0) return;

        var dotBrush = Application.Current?.TryFindResource("XD_GridDotBrush") as Brush
                       ?? new SolidColorBrush(Color.FromArgb(60, 120, 120, 120));

        double w = ActualWidth;
        double h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        // Phase-shift so the grid is anchored to the canvas origin (0,0) regardless of pan offset.
        double phaseX = ((OffsetX % cellPx) + cellPx) % cellPx;
        double phaseY = ((OffsetY % cellPx) + cellPx) % cellPx;

        double dotRadius = Math.Clamp(cellPx * 0.06, 0.5, 2.0);
        var pen = new Pen(dotBrush, dotRadius * 2) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        pen.Freeze();

        for (double x = phaseX; x < w; x += cellPx)
            for (double y = phaseY; y < h; y += cellPx)
                dc.DrawLine(pen, new Point(x, y), new Point(x, y)); // zero-length line = dot
    }
}
