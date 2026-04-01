// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: SnapGuideOverlay.cs
// Author: Derek Tremblay
// Created: 2026-03-17
// Description:
//     Adorner drawn above the entire design canvas that renders snap guide lines
//     during drag-move and resize operations. Lines fade out automatically after
//     the configured hold duration.
//
// Architecture Notes:
//     Adorner pattern — lives on the AdornerLayer of the DesignCanvas root.
//     Non-hit-testable (purely decorative).
//     DispatcherTimer drives the 800ms auto-fade after the last update.
// ==========================================================

using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using WpfHexEditor.Editor.XamlDesigner.Services;

namespace WpfHexEditor.Editor.XamlDesigner.Controls;

/// <summary>
/// Renders temporary snap guide lines on top of the design canvas during drag operations.
/// </summary>
public sealed class SnapGuideOverlay : Adorner
{
    private IReadOnlyList<SnapGuide> _guides = Array.Empty<SnapGuide>();
    private readonly DispatcherTimer _fadeTimer;

    public SnapGuideOverlay(UIElement adornedElement) : base(adornedElement)
    {
        IsHitTestVisible = false;

        _fadeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
        _fadeTimer.Tick += (_, _) =>
        {
            _fadeTimer.Stop();
            _guides = Array.Empty<SnapGuide>();
            InvalidateVisual();
        };
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Updates the displayed guide lines and resets the fade timer.
    /// Pass an empty list or null to hide all guides immediately.
    /// </summary>
    public void ShowGuides(IReadOnlyList<SnapGuide>? guides)
    {
        _guides = guides ?? Array.Empty<SnapGuide>();
        _fadeTimer.Stop();

        if (_guides.Count > 0)
            _fadeTimer.Start();

        InvalidateVisual();
    }

    /// <summary>Immediately clears all guides without waiting for fade.</summary>
    public void Clear()
    {
        _fadeTimer.Stop();
        _guides = Array.Empty<SnapGuide>();
        InvalidateVisual();
    }

    // ── Rendering ─────────────────────────────────────────────────────────────

    protected override void OnRender(DrawingContext dc)
    {
        if (_guides.Count == 0) return;

        var brush = Application.Current?.TryFindResource("XD_SnapGuideBrush") as Brush
                    ?? new SolidColorBrush(Color.FromArgb(200, 255, 0, 100));

        var pen = new Pen(brush, 1.0) { DashStyle = DashStyles.Dash };
        pen.Freeze();

        var bounds = new Rect(AdornedElement.RenderSize);

        var labelBg = Application.Current?.TryFindResource("XD_MeasureLabelBackground") as Brush
                      ?? new SolidColorBrush(Color.FromArgb(180, 30, 30, 30));

        foreach (var guide in _guides)
        {
            if (guide.IsVertical)
                dc.DrawLine(pen,
                    new Point(guide.Position, bounds.Top),
                    new Point(guide.Position, bounds.Bottom));
            else
                dc.DrawLine(pen,
                    new Point(bounds.Left,  guide.Position),
                    new Point(bounds.Right, guide.Position));

            // Distance label badge.
            if (!string.IsNullOrEmpty(guide.DistanceLabel))
            {
                var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
                var ft  = new FormattedText(
                    guide.DistanceLabel,
                    CultureInfo.CurrentUICulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"),
                    9.0,
                    brush,
                    dpi);

                const double padH = 3, padV = 1;
                double lw = ft.Width + padH * 2;
                double lh = ft.Height + padV * 2;

                double lx, ly;
                if (guide.IsVertical)
                {
                    lx = guide.Position + 3;
                    ly = bounds.Top + (bounds.Height - lh) / 2.0;
                }
                else
                {
                    lx = bounds.Left + (bounds.Width - lw) / 2.0;
                    ly = guide.Position + 3;
                }

                dc.DrawRoundedRectangle(labelBg, null, new Rect(lx, ly, lw, lh), 2, 2);
                dc.DrawText(ft, new Point(lx + padH, ly + padV));
            }
        }
    }
}
