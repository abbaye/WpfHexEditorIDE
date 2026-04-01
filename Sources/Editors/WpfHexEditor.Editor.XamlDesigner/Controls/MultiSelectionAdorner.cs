// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: MultiSelectionAdorner.cs
// Author: Derek Tremblay
// Created: 2026-03-17
// Description:
//     Adorner that draws a combined bounding rectangle around multiple selected
//     elements on the design canvas. Displayed in addition to the per-element
//     ResizeAdorner when more than one element is selected.
//
// Architecture Notes:
//     Adorner on the DesignCanvas root.
//     Non-hit-testable for individual resize, but handles move drag on the body.
//     Refreshed via Refresh(IEnumerable<UIElement> selection).
// ==========================================================

using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace WpfHexEditor.Editor.XamlDesigner.Controls;

/// <summary>
/// Shows a dashed bounding box around the combined extent of all selected elements,
/// plus a "N selected" count badge in the top-left corner.
/// </summary>
public sealed class MultiSelectionAdorner : Adorner
{
    private Rect _combinedBounds = Rect.Empty;
    private int  _count;

    public MultiSelectionAdorner(UIElement adornedElement) : base(adornedElement)
    {
        IsHitTestVisible = false;
    }

    /// <summary>
    /// Recomputes the combined bounding box from the given selection and redraws.
    /// </summary>
    public void Refresh(IEnumerable<UIElement> selection)
    {
        _combinedBounds = Rect.Empty;
        _count          = 0;

        foreach (var el in selection)
        {
            if (el is not FrameworkElement fe) continue;
            var pos = fe.TranslatePoint(new Point(0, 0), AdornedElement);
            var rect = new Rect(pos.X, pos.Y, fe.ActualWidth, fe.ActualHeight);
            _combinedBounds = _combinedBounds.IsEmpty ? rect : Rect.Union(_combinedBounds, rect);
            _count++;
        }

        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (_combinedBounds.IsEmpty || _combinedBounds.Width < 2) return;

        var borderBrush = Application.Current?.TryFindResource("XD_SelectionBorderBrush") as Brush
                          ?? new SolidColorBrush(Color.FromRgb(0, 122, 204));

        var pen = new Pen(borderBrush, 2.0) { DashStyle = new DashStyle(new double[] { 6, 3 }, 0) };
        pen.Freeze();

        // Outer bounding rect.
        dc.DrawRectangle(null, pen, _combinedBounds);

        // Corner markers.
        var handleFill = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255));
        handleFill.Freeze();
        double hs = 6;

        foreach (var corner in new[]
        {
            new Point(_combinedBounds.Left,                          _combinedBounds.Top),
            new Point(_combinedBounds.Left + _combinedBounds.Width,  _combinedBounds.Top),
            new Point(_combinedBounds.Left,                          _combinedBounds.Top + _combinedBounds.Height),
            new Point(_combinedBounds.Left + _combinedBounds.Width,  _combinedBounds.Top + _combinedBounds.Height),
        })
        {
            dc.DrawRectangle(handleFill, new Pen(borderBrush, 1.0),
                new Rect(corner.X - hs / 2, corner.Y - hs / 2, hs, hs));
        }

        // ── "N selected" count badge ──────────────────────────────────────────
        if (_count > 1)
        {
            var badgeText = new FormattedText(
                $"{_count} selected",
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                10.0,
                Brushes.White,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            const double padH = 5, padV = 2;
            double badgeW = badgeText.Width  + padH * 2;
            double badgeH = badgeText.Height + padV * 2;

            var badgeBg = Application.Current?.TryFindResource("XD_SelectionCountBadgeBrush") as Brush
                          ?? new SolidColorBrush(Color.FromArgb(204, 0, 122, 204));

            double bx = _combinedBounds.Left;
            double by = _combinedBounds.Top - badgeH - 2;
            if (by < 0) by = _combinedBounds.Top + 2;  // clamp inside canvas when near top

            dc.DrawRoundedRectangle(badgeBg, null, new Rect(bx, by, badgeW, badgeH), 3, 3);
            dc.DrawText(badgeText, new Point(bx + padH, by + padV));
        }
    }
}
