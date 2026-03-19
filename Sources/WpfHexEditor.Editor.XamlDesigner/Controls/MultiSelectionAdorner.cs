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

using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace WpfHexEditor.Editor.XamlDesigner.Controls;

/// <summary>
/// Shows a dashed bounding box around the combined extent of all selected elements.
/// </summary>
public sealed class MultiSelectionAdorner : Adorner
{
    private Rect _combinedBounds = Rect.Empty;

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

        foreach (var el in selection)
        {
            if (el is not FrameworkElement fe) continue;
            var pos = fe.TranslatePoint(new Point(0, 0), AdornedElement);
            var rect = new Rect(pos.X, pos.Y, fe.ActualWidth, fe.ActualHeight);
            _combinedBounds = _combinedBounds.IsEmpty ? rect : Rect.Union(_combinedBounds, rect);
        }

        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (_combinedBounds.IsEmpty || _combinedBounds.Width < 2) return;

        var brush = Application.Current?.TryFindResource("XD_SelectionBorderBrush") as Brush
                    ?? new SolidColorBrush(Color.FromRgb(0, 122, 204));

        var pen = new Pen(brush, 2.0) { DashStyle = new DashStyle(new double[] { 6, 3 }, 0) };
        pen.Freeze();

        // Outer bounding rect.
        dc.DrawRectangle(null, pen, _combinedBounds);

        // Corner markers.
        var fill = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255));
        fill.Freeze();
        double hs = 6;

        foreach (var corner in new[]
        {
            new Point(_combinedBounds.Left,                      _combinedBounds.Top),
            new Point(_combinedBounds.Left + _combinedBounds.Width, _combinedBounds.Top),
            new Point(_combinedBounds.Left,                      _combinedBounds.Top + _combinedBounds.Height),
            new Point(_combinedBounds.Left + _combinedBounds.Width, _combinedBounds.Top + _combinedBounds.Height),
        })
        {
            dc.DrawRectangle(fill, new Pen(brush, 1.0) { },
                new Rect(corner.X - hs / 2, corner.Y - hs / 2, hs, hs));
        }
    }
}
