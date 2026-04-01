// ==========================================================
// Project: WpfHexEditor.ProgressBar
// File: Controls/LinearProgressBar.cs
// Description:
//     Classic horizontal progress bar with rounded corners.
//     Supports determinate fill, indeterminate slug animation,
//     and percentage text overlay. Pure OnRender — no templates.
// ==========================================================

using System.Windows;
using System.Windows.Media;

namespace WpfHexEditor.ProgressBar.Controls;

/// <summary>
/// Lightweight horizontal progress bar rendered via <see cref="OnRender"/>.
/// </summary>
public sealed class LinearProgressBar : ProgressBarBase
{
    // ── Additional DPs ────────────────────────────────────────────────────────

    public static readonly DependencyProperty CornerRadiusProperty =
        DependencyProperty.Register(nameof(CornerRadius), typeof(double), typeof(LinearProgressBar),
            new FrameworkPropertyMetadata(2.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BarHeightProperty =
        DependencyProperty.Register(nameof(BarHeight), typeof(double), typeof(LinearProgressBar),
            new FrameworkPropertyMetadata(4.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Corner radius for the track and fill rectangles.</summary>
    public double CornerRadius
    {
        get => (double)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    /// <summary>Height of the bar. Defaults to 4px (thin).</summary>
    public double BarHeight
    {
        get => (double)GetValue(BarHeightProperty);
        set => SetValue(BarHeightProperty, value);
    }

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

        var cr = CornerRadius;
        var trackBrush = ResolveTrackBrush();
        var fillBrush  = ResolveActiveBrush();

        // Track (full width)
        dc.DrawRoundedRectangle(trackBrush, null, new Rect(0, 0, w, h), cr, cr);

        if (IsIndeterminate)
        {
            // Sliding slug (30% width)
            var slugWidth = w * 0.3;
            var offset = IndeterminateOffset;
            // Ease: accelerate in center, decelerate at edges
            var t = Math.Sin(offset * Math.PI);
            var x = t * (w - slugWidth);

            dc.PushClip(new RectangleGeometry(new Rect(0, 0, w, h), cr, cr));
            dc.DrawRoundedRectangle(fillBrush, null, new Rect(x, 0, slugWidth, h), cr, cr);
            dc.Pop();
        }
        else if (AnimatedProgress > 0)
        {
            // Determinate fill
            var fillWidth = Math.Max(cr * 2, AnimatedProgress * w);
            dc.PushClip(new RectangleGeometry(new Rect(0, 0, w, h), cr, cr));
            dc.DrawRoundedRectangle(fillBrush, null, new Rect(0, 0, fillWidth, h), cr, cr);
            dc.Pop();
        }

        // Percentage text overlay
        if (ShowPercentage && !IsIndeterminate)
        {
            var ft = CreatePercentageText(h > 12 ? 10 : h * 0.7);
            var textX = (w - ft.Width) / 2;
            var textY = (h - ft.Height) / 2;
            dc.DrawText(ft, new Point(textX, textY));
        }
    }
}
