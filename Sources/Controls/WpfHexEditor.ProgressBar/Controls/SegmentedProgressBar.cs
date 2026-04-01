// ==========================================================
// Project: WpfHexEditor.ProgressBar
// File: Controls/SegmentedProgressBar.cs
// Description:
//     Horizontal bar divided into N segments that fill sequentially.
//     Useful for multi-step or phased operations.
//     Pure OnRender — no templates.
// ==========================================================

using System.Windows;
using System.Windows.Media;

namespace WpfHexEditor.ProgressBar.Controls;

/// <summary>
/// Segmented horizontal progress bar. Each segment fills independently
/// based on <see cref="ProgressBarBase.Progress"/>.
/// </summary>
public sealed class SegmentedProgressBar : ProgressBarBase
{
    // ── Additional DPs ────────────────────────────────────────────────────────

    public static readonly DependencyProperty SegmentCountProperty =
        DependencyProperty.Register(nameof(SegmentCount), typeof(int), typeof(SegmentedProgressBar),
            new FrameworkPropertyMetadata(5, FrameworkPropertyMetadataOptions.AffectsRender, null, CoerceSegmentCount));

    public static readonly DependencyProperty SegmentSpacingProperty =
        DependencyProperty.Register(nameof(SegmentSpacing), typeof(double), typeof(SegmentedProgressBar),
            new FrameworkPropertyMetadata(2.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CornerRadiusProperty =
        DependencyProperty.Register(nameof(CornerRadius), typeof(double), typeof(SegmentedProgressBar),
            new FrameworkPropertyMetadata(2.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BarHeightProperty =
        DependencyProperty.Register(nameof(BarHeight), typeof(double), typeof(SegmentedProgressBar),
            new FrameworkPropertyMetadata(6.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Number of segments. Minimum 1.</summary>
    public int SegmentCount
    {
        get => (int)GetValue(SegmentCountProperty);
        set => SetValue(SegmentCountProperty, value);
    }

    /// <summary>Horizontal spacing between segments in pixels.</summary>
    public double SegmentSpacing
    {
        get => (double)GetValue(SegmentSpacingProperty);
        set => SetValue(SegmentSpacingProperty, value);
    }

    /// <summary>Corner radius for each segment rectangle.</summary>
    public double CornerRadius
    {
        get => (double)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    /// <summary>Height of the segmented bar.</summary>
    public double BarHeight
    {
        get => (double)GetValue(BarHeightProperty);
        set => SetValue(BarHeightProperty, value);
    }

    private static object CoerceSegmentCount(DependencyObject d, object value)
        => Math.Max(1, (int)value);

    // ── Layout ────────────────────────────────────────────────────────────────

    protected override Size MeasureOverride(Size availableSize)
    {
        var h = double.IsNaN(Height) ? BarHeight : Height;
        var w = double.IsNaN(Width) ? availableSize.Width : Width;
        return new Size(w, h);
    }

    // ── Render ────────────────────────────────────────────────────────────────

    protected override void OnRender(DrawingContext dc)
    {
        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        int n        = SegmentCount;
        double gap   = SegmentSpacing;
        double cr    = CornerRadius;
        double segW  = (w - gap * (n - 1)) / n;
        if (segW <= 0) return;

        var trackBrush = ResolveTrackBrush();
        var fillBrush  = ResolveActiveBrush();

        double progress = IsIndeterminate ? 0 : AnimatedProgress;
        double filledSegments = progress * n;

        for (int i = 0; i < n; i++)
        {
            double x = i * (segW + gap);
            var rect = new Rect(x, 0, segW, h);

            // Track
            dc.DrawRoundedRectangle(trackBrush, null, rect, cr, cr);

            if (IsIndeterminate)
            {
                // Pulse: one segment "lights up" and sweeps
                double pulseCenter = IndeterminateOffset * n;
                double dist = Math.Abs(i - pulseCenter);
                if (dist > n / 2.0) dist = n - dist; // wrap around
                double alpha = Math.Max(0, 1.0 - dist / 2.0);
                if (alpha > 0.05)
                {
                    var pulseBrush = fillBrush.Clone();
                    pulseBrush.Opacity = alpha;
                    dc.DrawRoundedRectangle(pulseBrush, null, rect, cr, cr);
                }
            }
            else if (i < (int)filledSegments)
            {
                // Fully filled segment
                dc.DrawRoundedRectangle(fillBrush, null, rect, cr, cr);
            }
            else if (i < filledSegments)
            {
                // Partially filled segment
                double partial = filledSegments - (int)filledSegments;
                var clipRect = new Rect(x, 0, segW * partial, h);
                dc.PushClip(new RectangleGeometry(clipRect));
                dc.DrawRoundedRectangle(fillBrush, null, rect, cr, cr);
                dc.Pop();
            }
        }

        // Percentage text
        if (ShowPercentage && !IsIndeterminate)
        {
            var ft = CreatePercentageText(h > 12 ? 10 : h * 0.7);
            dc.DrawText(ft, new Point((w - ft.Width) / 2, (h - ft.Height) / 2));
        }
    }
}
