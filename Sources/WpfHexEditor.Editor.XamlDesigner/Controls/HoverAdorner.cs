// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: HoverAdorner.cs
// Author: Derek Tremblay
// Created: 2026-03-18
// Description:
//     Lightweight adorner that draws a thin solid rectangle around the element
//     currently under the mouse cursor in the design canvas.
//     Provides VS-like hover preview — indicates which element would be selected
//     on a mouse click, without requiring an actual click.
//
// Architecture Notes:
//     IsHitTestVisible = false — purely decorative, never intercepts mouse events.
//     Theme-aware via XD_HoverBorderBrush resource token.
//     Managed exclusively by DesignCanvas (added/removed on MouseMove/MouseLeave).
// ==========================================================

using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace WpfHexEditor.Editor.XamlDesigner.Controls;

/// <summary>
/// Decorative adorner that draws a thin 1px solid rectangle around the element
/// currently under the mouse cursor, previewing the click-to-select target.
/// </summary>
public sealed class HoverAdorner : Adorner
{
    public HoverAdorner(UIElement adornedElement) : base(adornedElement)
    {
        IsHitTestVisible = false; // never intercepts mouse events
    }

    protected override void OnRender(DrawingContext dc)
    {
        var brush = Application.Current?.TryFindResource("XD_HoverBorderBrush") as Brush
                    ?? new SolidColorBrush(Color.FromArgb(180, 0, 160, 255));

        var pen = new Pen(brush, 1.5);
        pen.Freeze();

        var bounds = new Rect(AdornedElement.RenderSize);
        dc.DrawRectangle(
            null, pen,
            new Rect(
                bounds.X + 0.75,
                bounds.Y + 0.75,
                Math.Max(0, bounds.Width  - 1.5),
                Math.Max(0, bounds.Height - 1.5)));
    }
}
