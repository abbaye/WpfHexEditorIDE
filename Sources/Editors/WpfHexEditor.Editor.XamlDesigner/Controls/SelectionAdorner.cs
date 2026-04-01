// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: SelectionAdorner.cs
// Author: Derek Tremblay
// Created: 2026-03-16
// Description:
//     Adorner drawn over the selected element on the design canvas.
//     Phase 1: dashed blue border, no resize handles.
//     Phase 2: adds 8 hit-testable resize handles + mouse drag for move/resize.
//
// Architecture Notes:
//     Inherits Adorner (System.Windows.Documents). Rendered via OnRender.
//     Brush resolved from XD_SelectionBorderBrush theme token at render time.
// ==========================================================

using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace WpfHexEditor.Editor.XamlDesigner.Controls;

/// <summary>
/// Dashed-border adorner rendered on top of the currently selected element
/// in the XAML design canvas.
/// </summary>
public sealed class SelectionAdorner : Adorner
{
    private static readonly DashStyle _dashStyle = new(new double[] { 4, 2 }, 0);

    public SelectionAdorner(UIElement adornedElement)
        : base(adornedElement)
    {
        // Phase 1: not hit-testable — selection is driven by mouse clicks on the canvas.
        IsHitTestVisible = false;
    }

    protected override void OnRender(DrawingContext dc)
    {
        // Resolve theme-aware selection brush; fall back to VS blue.
        var brush = Application.Current?.TryFindResource("XD_SelectionBorderBrush") as Brush
                    ?? new SolidColorBrush(Color.FromRgb(0, 122, 204));

        var pen = new Pen(brush, 2.0) { DashStyle = _dashStyle };
        pen.Freeze();

        var bounds = new Rect(AdornedElement.RenderSize);

        // Inset by half the pen width so the stroke stays fully inside the element bounds.
        var inset = new Rect(
            bounds.X      + 1.0,
            bounds.Y      + 1.0,
            Math.Max(0, bounds.Width  - 2.0),
            Math.Max(0, bounds.Height - 2.0));

        dc.DrawRectangle(null, pen, inset);
    }
}
