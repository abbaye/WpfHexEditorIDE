// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: RulerControl.cs
// Author: Derek Tremblay
// Created: 2026-03-17
// Description:
//     Pixel ruler drawn along the top or left edge of the design canvas.
//     Renders tick marks and numeric labels at graduated intervals.
//     A red cursor line tracks the current mouse position.
//
// Architecture Notes:
//     FrameworkElement — rendered entirely via DrawingVisual in OnRender.
//     Orientation property selects Horizontal vs Vertical layout.
//     ZoomFactor and Offset updated by ZoomPanCanvas for accurate display.
// ==========================================================

using System.Windows;
using System.Windows.Media;

namespace WpfHexEditor.Editor.XamlDesigner.Controls;

/// <summary>
/// Horizontal or vertical pixel ruler for the XAML design canvas.
/// </summary>
public sealed class RulerControl : FrameworkElement
{
    // ── Dependency Properties ─────────────────────────────────────────────────

    public static readonly DependencyProperty IsHorizontalProperty =
        DependencyProperty.Register(nameof(IsHorizontal), typeof(bool), typeof(RulerControl),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ZoomFactorProperty =
        DependencyProperty.Register(nameof(ZoomFactor), typeof(double), typeof(RulerControl),
            new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty OffsetProperty =
        DependencyProperty.Register(nameof(Offset), typeof(double), typeof(RulerControl),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CursorPositionProperty =
        DependencyProperty.Register(nameof(CursorPosition), typeof(double), typeof(RulerControl),
            new FrameworkPropertyMetadata(-1.0, FrameworkPropertyMetadataOptions.AffectsRender));

    // ── Properties ────────────────────────────────────────────────────────────

    public bool   IsHorizontal   { get => (bool)GetValue(IsHorizontalProperty);    set => SetValue(IsHorizontalProperty,   value); }
    public double ZoomFactor     { get => (double)GetValue(ZoomFactorProperty);    set => SetValue(ZoomFactorProperty,     value); }
    public double Offset         { get => (double)GetValue(OffsetProperty);        set => SetValue(OffsetProperty,         value); }
    public double CursorPosition { get => (double)GetValue(CursorPositionProperty);set => SetValue(CursorPositionProperty, value); }

    // ── Rendering ─────────────────────────────────────────────────────────────

    protected override void OnRender(DrawingContext dc)
    {
        var bgBrush   = Application.Current?.TryFindResource("XD_RulerBackground") as Brush
                        ?? new SolidColorBrush(Color.FromRgb(42, 42, 46));
        var tickBrush = Application.Current?.TryFindResource("XD_RulerTickBrush") as Brush
                        ?? new SolidColorBrush(Color.FromRgb(140, 140, 140));
        var cursorBrush = Application.Current?.TryFindResource("XD_RulerCursorBrush") as Brush
                          ?? Brushes.OrangeRed;

        var size = new Size(ActualWidth, ActualHeight);
        dc.DrawRectangle(bgBrush, null, new Rect(size));

        DrawTicks(dc, size, tickBrush);
        DrawCursorLine(dc, size, cursorBrush);
    }

    private void DrawTicks(DrawingContext dc, Size size, Brush tickBrush)
    {
        double zoom   = Math.Max(0.01, ZoomFactor);
        double offset = Offset;
        double length = IsHorizontal ? size.Width : size.Height;
        double thickness = IsHorizontal ? size.Height : size.Width;

        // Choose tick interval based on zoom level.
        double[] intervals = { 1, 2, 5, 10, 20, 25, 50, 100, 200, 250, 500, 1000 };
        double targetPixelsBetweenLabels = 80.0;
        double interval = intervals.FirstOrDefault(
            i => i * zoom >= targetPixelsBetweenLabels / 5.0, 100.0);

        var smallPen = new Pen(tickBrush, 0.5); smallPen.Freeze();
        var bigPen   = new Pen(tickBrush, 1.0); bigPen.Freeze();

        var typeface = new Typeface("Segoe UI");
        double fontSize = 9.0;

        double firstTick = Math.Floor(-offset / (interval * zoom)) * interval;

        for (double val = firstTick; val * zoom + offset < length + interval * zoom; val += interval)
        {
            double pos = val * zoom + offset;
            if (pos < 0 || pos > length) continue;

            bool isLabel = ((int)Math.Round(val) % (int)(interval * 5)) == 0
                           || interval >= 50;

            double tickLen = isLabel ? thickness * 0.55 : thickness * 0.3;
            var pen = isLabel ? bigPen : smallPen;

            if (IsHorizontal)
            {
                dc.DrawLine(pen, new Point(pos, thickness - tickLen), new Point(pos, thickness));
                if (isLabel)
                {
                    var ft = new FormattedText(
                        ((int)val).ToString(),
                        System.Globalization.CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight,
                        typeface, fontSize, tickBrush,
                        VisualTreeHelper.GetDpi(this).PixelsPerDip);
                    dc.DrawText(ft, new Point(pos + 2, 1));
                }
            }
            else
            {
                dc.DrawLine(pen, new Point(thickness - tickLen, pos), new Point(thickness, pos));
                if (isLabel)
                {
                    var ft = new FormattedText(
                        ((int)val).ToString(),
                        System.Globalization.CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight,
                        typeface, fontSize, tickBrush,
                        VisualTreeHelper.GetDpi(this).PixelsPerDip);

                    // Rotate the label 90° for vertical ruler.
                    dc.PushTransform(new RotateTransform(-90, 1, pos - 2));
                    dc.DrawText(ft, new Point(1, pos - 2));
                    dc.Pop();
                }
            }
        }
    }

    private void DrawCursorLine(DrawingContext dc, Size size, Brush cursorBrush)
    {
        double pos = CursorPosition;
        if (pos < 0) return;

        var pen = new Pen(cursorBrush, 1.0); pen.Freeze();

        if (IsHorizontal)
            dc.DrawLine(pen, new Point(pos, 0), new Point(pos, size.Height));
        else
            dc.DrawLine(pen, new Point(0, pos), new Point(size.Width, pos));
    }
}
