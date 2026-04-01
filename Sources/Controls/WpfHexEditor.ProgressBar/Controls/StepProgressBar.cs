// ==========================================================
// Project: WpfHexEditor.ProgressBar
// File: Controls/StepProgressBar.cs
// Description:
//     Horizontal wizard-style step indicator with numbered
//     circles connected by lines. Completed steps show a
//     checkmark; current step pulses. Pure OnRender.
// ==========================================================

using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using WpfHexEditor.ProgressBar.Helpers;

namespace WpfHexEditor.ProgressBar.Controls;

/// <summary>
/// Step progress indicator for wizard/workflow UIs.
/// Each step is a circle connected by a horizontal line.
/// </summary>
public sealed class StepProgressBar : ProgressBarBase
{
    // ── Additional DPs ────────────────────────────────────────────────────────

    public static readonly DependencyProperty StepCountProperty =
        DependencyProperty.Register(nameof(StepCount), typeof(int), typeof(StepProgressBar),
            new FrameworkPropertyMetadata(4, FrameworkPropertyMetadataOptions.AffectsRender, null, CoerceStepCount));

    public static readonly DependencyProperty CurrentStepProperty =
        DependencyProperty.Register(nameof(CurrentStep), typeof(int), typeof(StepProgressBar),
            new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StepLabelsProperty =
        DependencyProperty.Register(nameof(StepLabels), typeof(IList<string>), typeof(StepProgressBar),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StepRadiusProperty =
        DependencyProperty.Register(nameof(StepRadius), typeof(double), typeof(StepProgressBar),
            new FrameworkPropertyMetadata(12.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ConnectorHeightProperty =
        DependencyProperty.Register(nameof(ConnectorHeight), typeof(double), typeof(StepProgressBar),
            new FrameworkPropertyMetadata(2.0, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Total number of steps.</summary>
    public int StepCount
    {
        get => (int)GetValue(StepCountProperty);
        set => SetValue(StepCountProperty, value);
    }

    /// <summary>Zero-based index of the current step. Steps &lt; CurrentStep are completed.</summary>
    public int CurrentStep
    {
        get => (int)GetValue(CurrentStepProperty);
        set => SetValue(CurrentStepProperty, value);
    }

    /// <summary>Optional labels displayed below each step circle.</summary>
    public IList<string>? StepLabels
    {
        get => (IList<string>?)GetValue(StepLabelsProperty);
        set => SetValue(StepLabelsProperty, value);
    }

    /// <summary>Radius of each step circle.</summary>
    public double StepRadius
    {
        get => (double)GetValue(StepRadiusProperty);
        set => SetValue(StepRadiusProperty, value);
    }

    /// <summary>Height of the connector line between circles.</summary>
    public double ConnectorHeight
    {
        get => (double)GetValue(ConnectorHeightProperty);
        set => SetValue(ConnectorHeightProperty, value);
    }

    private static object CoerceStepCount(DependencyObject d, object value)
        => Math.Max(1, (int)value);

    // ── Layout ────────────────────────────────────────────────────────────────

    protected override Size MeasureOverride(Size availableSize)
    {
        var r = StepRadius;
        var h = r * 2 + (StepLabels is not null ? 16 : 0);
        var w = double.IsNaN(Width) ? availableSize.Width : Width;
        return new Size(w, h);
    }

    // ── Render ────────────────────────────────────────────────────────────────

    private static readonly Color DefaultSuccessColor = (Color)ColorConverter.ConvertFromString("#60A917");

    protected override void OnRender(DrawingContext dc)
    {
        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        int n      = StepCount;
        double r   = StepRadius;
        double cy  = r; // circle center Y
        var labels = StepLabels;
        var dpi    = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        var trackBrush   = ResolveTrackBrush();
        var activeBrush  = ResolveActiveBrush();
        var successBrush = ThemeBrushHelper.Resolve(this, SuccessBrush, "", DefaultSuccessColor);
        var fgBrush      = ResolvePercentageForeground();

        // Compute step X positions (evenly distributed)
        double margin = r;
        double span   = w - 2 * margin;
        double stepGap = n > 1 ? span / (n - 1) : 0;

        var connPen = new Pen(trackBrush, ConnectorHeight) { StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat };
        var connDonePen = new Pen(successBrush, ConnectorHeight) { StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat };
        if (connPen.CanFreeze) connPen.Freeze();
        if (connDonePen.CanFreeze) connDonePen.Freeze();

        // Draw connectors first (behind circles)
        for (int i = 0; i < n - 1; i++)
        {
            double x1 = margin + i * stepGap + r;
            double x2 = margin + (i + 1) * stepGap - r;
            var pen = i < CurrentStep ? connDonePen : connPen;
            dc.DrawLine(pen, new Point(x1, cy), new Point(x2, cy));
        }

        // Draw circles
        for (int i = 0; i < n; i++)
        {
            double cx = margin + i * stepGap;
            var center = new Point(cx, cy);

            Brush circleFill;
            if (i < CurrentStep)
                circleFill = successBrush;
            else if (i == CurrentStep)
                circleFill = activeBrush;
            else
                circleFill = trackBrush;

            dc.DrawEllipse(circleFill, null, center, r, r);

            // Inner content
            if (i < CurrentStep)
            {
                // Checkmark glyph ✓
                var check = new FormattedText("\uE73E", CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight, new Typeface("Segoe MDL2 Assets"),
                    r * 0.9, Brushes.White, dpi);
                dc.DrawText(check, new Point(cx - check.Width / 2, cy - check.Height / 2));
            }
            else
            {
                // Step number
                var num = new FormattedText((i + 1).ToString(), CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight, new Typeface("Segoe UI"),
                    r * 0.85, Brushes.White, dpi);
                dc.DrawText(num, new Point(cx - num.Width / 2, cy - num.Height / 2));
            }

            // Label below
            if (labels is not null && i < labels.Count && !string.IsNullOrEmpty(labels[i]))
            {
                var label = new FormattedText(labels[i], CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight, new Typeface("Segoe UI"),
                    10, fgBrush, dpi);
                dc.DrawText(label, new Point(cx - label.Width / 2, cy + r + 4));
            }
        }
    }
}
