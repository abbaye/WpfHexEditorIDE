// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: TimelineRuler.cs
// Author: Derek Tremblay
// Created: 2026-03-17
// Description:
//     Horizontal time ruler for the animation timeline panel.
//     Renders tick marks in milliseconds/seconds with a draggable
//     playhead cursor at the current playback position.
//
// Architecture Notes:
//     FrameworkElement — DrawingVisual rendering in OnRender.
//     Duration and CurrentTime DPs drive playhead position.
//     MouseDown raises SeekRequested event for playback scrubbing.
// ==========================================================

using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace WpfHexEditor.Editor.XamlDesigner.Controls;

/// <summary>
/// Time ruler bar for the animation timeline panel.
/// </summary>
public sealed class TimelineRuler : FrameworkElement
{
    // ── Dependency Properties ─────────────────────────────────────────────────

    public static readonly DependencyProperty DurationProperty =
        DependencyProperty.Register(nameof(Duration), typeof(TimeSpan), typeof(TimelineRuler),
            new FrameworkPropertyMetadata(TimeSpan.FromSeconds(2), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CurrentTimeProperty =
        DependencyProperty.Register(nameof(CurrentTime), typeof(TimeSpan), typeof(TimelineRuler),
            new FrameworkPropertyMetadata(TimeSpan.Zero, FrameworkPropertyMetadataOptions.AffectsRender));

    public TimeSpan Duration    { get => (TimeSpan)GetValue(DurationProperty);    set => SetValue(DurationProperty, value); }
    public TimeSpan CurrentTime { get => (TimeSpan)GetValue(CurrentTimeProperty); set => SetValue(CurrentTimeProperty, value); }

    // ── Events ────────────────────────────────────────────────────────────────

    public event EventHandler<TimeSpan>? SeekRequested;

    // ── Input ─────────────────────────────────────────────────────────────────

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        double ratio = Math.Clamp(e.GetPosition(this).X / ActualWidth, 0, 1);
        SeekRequested?.Invoke(this, TimeSpan.FromMilliseconds(Duration.TotalMilliseconds * ratio));
        CaptureMouse();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (!IsMouseCaptured) return;
        double ratio = Math.Clamp(e.GetPosition(this).X / ActualWidth, 0, 1);
        SeekRequested?.Invoke(this, TimeSpan.FromMilliseconds(Duration.TotalMilliseconds * ratio));
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        ReleaseMouseCapture();
    }

    // ── Rendering ─────────────────────────────────────────────────────────────

    protected override void OnRender(DrawingContext dc)
    {
        var bgBrush   = Application.Current?.TryFindResource("XD_TimelineBackground") as Brush
                        ?? new SolidColorBrush(Color.FromRgb(30, 30, 30));
        var tickBrush = Application.Current?.TryFindResource("XD_RulerTickBrush") as Brush
                        ?? new SolidColorBrush(Color.FromRgb(140, 140, 140));
        var playBrush = Application.Current?.TryFindResource("XD_RulerCursorBrush") as Brush
                        ?? Brushes.OrangeRed;

        var size = new Size(ActualWidth, ActualHeight);
        dc.DrawRectangle(bgBrush, null, new Rect(size));

        double totalMs   = Duration.TotalMilliseconds;
        if (totalMs <= 0) return;

        // Draw ticks every 100ms, labels every 500ms.
        var smallPen = new Pen(tickBrush, 0.5); smallPen.Freeze();
        var bigPen   = new Pen(tickBrush, 1.0); bigPen.Freeze();
        var typeface = new Typeface("Segoe UI");

        for (double ms = 0; ms <= totalMs; ms += 100)
        {
            double x = ms / totalMs * size.Width;
            bool   isLabel = (int)ms % 500 == 0;

            double tickH = isLabel ? size.Height * 0.6 : size.Height * 0.3;
            dc.DrawLine(isLabel ? bigPen : smallPen,
                new Point(x, size.Height - tickH),
                new Point(x, size.Height));

            if (isLabel)
            {
                string label = ms >= 1000 ? $"{ms / 1000:F1}s" : $"{ms:F0}ms";
                var ft = new FormattedText(label,
                    System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    typeface, 9, tickBrush,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);
                dc.DrawText(ft, new Point(x + 2, 1));
            }
        }

        // Playhead.
        double playX = CurrentTime.TotalMilliseconds / totalMs * size.Width;
        var playPen  = new Pen(playBrush, 2.0); playPen.Freeze();
        dc.DrawLine(playPen, new Point(playX, 0), new Point(playX, size.Height));
    }
}
