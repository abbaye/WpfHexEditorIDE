// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: PerformanceOverlayAdorner.cs
// Description:
//     Fixed top-right overlay showing live canvas performance metrics:
//     FPS counter (color-coded green/yellow/red), last render time (ms),
//     element count, and max tree depth. Drawn outside the zoom transform
//     so the badge is always full-size at 1:1 regardless of zoom.
//
// Architecture Notes:
//     Adorner on DesignRoot but positioned relative to the ZoomPanCanvas
//     viewport rather than the adorned element's local coordinates.
//     Non-hit-testable. Updated by DesignCanvas via Refresh().
//     Theme-aware: XD_PerfOverlayBackground, XD_PerfOverlayForeground,
//                  XD_PerfFpsGoodBrush, XD_PerfFpsWarnBrush, XD_PerfFpsBadBrush.
// ==========================================================

using System.Diagnostics;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using WpfHexEditor.Editor.XamlDesigner.Models;

namespace WpfHexEditor.Editor.XamlDesigner.Controls;

/// <summary>
/// Non-interactive FPS + render-time overlay adorner.
/// </summary>
public sealed class PerformanceOverlayAdorner : Adorner
{
    private const double PadH   = 8.0;
    private const double PadV   = 4.0;
    private const double Width  = 160.0;
    private const double Height = 60.0;

    private DesignCanvasStats _stats = DesignCanvasStats.Empty;

    // FPS tracking via CompositionTarget.Rendering.
    private readonly Stopwatch _fpsWatch = Stopwatch.StartNew();
    private int    _frameCount;
    private double _currentFps;

    public PerformanceOverlayAdorner(UIElement adornedElement)
        : base(adornedElement)
    {
        IsHitTestVisible = false;
        CompositionTarget.Rendering += OnRendering;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Updates stats and triggers a redraw.</summary>
    public void Refresh(DesignCanvasStats stats)
    {
        _stats = stats;
        InvalidateVisual();
    }

    /// <summary>
    /// Detaches the <see cref="CompositionTarget.Rendering"/> subscription.
    /// Call when hiding or removing the adorner.
    /// </summary>
    public void Detach()
        => CompositionTarget.Rendering -= OnRendering;

    // ── Adorner overrides ─────────────────────────────────────────────────────

    protected override void OnRender(DrawingContext dc)
    {
        double ew = AdornedElement.RenderSize.Width;

        // Position badge at top-right of the adorned element.
        double x = ew - Width - 8;
        double y = 8;

        var bgBrush = Application.Current?.TryFindResource("XD_PerfOverlayBackground") as Brush
                      ?? new SolidColorBrush(Color.FromArgb(200, 20, 20, 20));
        var fgBrush = Application.Current?.TryFindResource("XD_PerfOverlayForeground") as Brush
                      ?? Brushes.White;

        var goodBrush = Application.Current?.TryFindResource("XD_PerfFpsGoodBrush") as Brush
                        ?? new SolidColorBrush(Color.FromRgb(0x6A, 0xDA, 0x6A));
        var warnBrush = Application.Current?.TryFindResource("XD_PerfFpsWarnBrush") as Brush
                        ?? new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00));
        var badBrush  = Application.Current?.TryFindResource("XD_PerfFpsBadBrush")  as Brush
                        ?? new SolidColorBrush(Color.FromRgb(0xFF, 0x55, 0x55));

        // Background badge.
        dc.DrawRoundedRectangle(bgBrush, null, new Rect(x, y, Width, Height), 4, 4);

        var typeface  = new Typeface("Consolas");
        double fs     = 10.5;
        var culture   = System.Globalization.CultureInfo.InvariantCulture;

        // FPS line — color by threshold.
        var fpsBrush = _currentFps >= 55 ? goodBrush
                     : _currentFps >= 30 ? warnBrush
                     : badBrush;

        var fpsText = new FormattedText(
            $"FPS   {_currentFps:F0}",
            culture, FlowDirection.LeftToRight, typeface, fs, fpsBrush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        var renderText = new FormattedText(
            $"Render {_stats.LastRenderMs:F1} ms",
            culture, FlowDirection.LeftToRight, typeface, fs, fgBrush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        var statsText = new FormattedText(
            $"Elem  {_stats.ElementCount}  Depth {_stats.MaxDepth}",
            culture, FlowDirection.LeftToRight, typeface, fs, fgBrush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        dc.DrawText(fpsText,    new Point(x + PadH, y + PadV));
        dc.DrawText(renderText, new Point(x + PadH, y + PadV + fs + 2));
        dc.DrawText(statsText,  new Point(x + PadH, y + PadV + (fs + 2) * 2));
    }

    // ── FPS counter ───────────────────────────────────────────────────────────

    private void OnRendering(object? sender, EventArgs e)
    {
        _frameCount++;
        if (_fpsWatch.Elapsed.TotalSeconds >= 0.5)
        {
            _currentFps = _frameCount / _fpsWatch.Elapsed.TotalSeconds;
            _frameCount = 0;
            _fpsWatch.Restart();
            InvalidateVisual();
        }
    }
}
