// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: ConstraintService.cs
// Description:
//     Reads and writes layout constraints (Margin, HorizontalAlignment,
//     VerticalAlignment) on FrameworkElements, mapping them to the
//     4-spring pin model used by Blend's constraint adorner.
//
// Architecture Notes:
//     Pure service — no WPF rendering dependency.
//     A "pinned" edge means the element's Margin on that side is fixed
//     and the element is aligned to that edge (not Stretch).
//     Toggling a pin flips between Auto/Stretch and the fixed margin.
// ==========================================================

using System.Windows;
using System.Windows.Controls;

namespace WpfHexEditor.Editor.XamlDesigner.Services;

/// <summary>
/// Which edges are pinned (fixed margin + edge alignment).
/// </summary>
[Flags]
public enum PinnedEdges
{
    None   = 0,
    Left   = 1 << 0,
    Top    = 1 << 1,
    Right  = 1 << 2,
    Bottom = 1 << 3,
}

/// <summary>
/// Reads and toggles layout-constraint pins on FrameworkElements.
/// </summary>
public sealed class ConstraintService
{
    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns which edges are currently pinned (fixed alignment) for <paramref name="element"/>.
    /// </summary>
    public PinnedEdges GetPinnedEdges(FrameworkElement element)
    {
        var pins = PinnedEdges.None;

        if (element.HorizontalAlignment == HorizontalAlignment.Left   ||
            element.HorizontalAlignment == HorizontalAlignment.Stretch)
            pins |= PinnedEdges.Left;

        if (element.HorizontalAlignment == HorizontalAlignment.Right  ||
            element.HorizontalAlignment == HorizontalAlignment.Stretch)
            pins |= PinnedEdges.Right;

        if (element.VerticalAlignment == VerticalAlignment.Top    ||
            element.VerticalAlignment == VerticalAlignment.Stretch)
            pins |= PinnedEdges.Top;

        if (element.VerticalAlignment == VerticalAlignment.Bottom ||
            element.VerticalAlignment == VerticalAlignment.Stretch)
            pins |= PinnedEdges.Bottom;

        return pins;
    }

    /// <summary>
    /// Toggles the pin state of <paramref name="edge"/> on <paramref name="element"/>.
    /// Returns the updated <see cref="PinnedEdges"/> after the toggle.
    /// </summary>
    public PinnedEdges TogglePin(FrameworkElement element, PinnedEdges edge)
    {
        var current = GetPinnedEdges(element);
        bool willPin = !current.HasFlag(edge);

        switch (edge)
        {
            case PinnedEdges.Left:
                element.HorizontalAlignment = willPin
                    ? HorizontalAlignment.Left
                    : HorizontalAlignment.Center;
                break;

            case PinnedEdges.Right:
                element.HorizontalAlignment = willPin
                    ? HorizontalAlignment.Right
                    : HorizontalAlignment.Center;
                break;

            case PinnedEdges.Top:
                element.VerticalAlignment = willPin
                    ? VerticalAlignment.Top
                    : VerticalAlignment.Center;
                break;

            case PinnedEdges.Bottom:
                element.VerticalAlignment = willPin
                    ? VerticalAlignment.Bottom
                    : VerticalAlignment.Center;
                break;
        }

        return GetPinnedEdges(element);
    }

    /// <summary>
    /// Returns the margin values for all four edges of <paramref name="element"/>.
    /// </summary>
    public Thickness GetMargin(FrameworkElement element) => element.Margin;

    /// <summary>
    /// Sets the margin for <paramref name="element"/> and returns the new thickness.
    /// </summary>
    public Thickness SetMargin(FrameworkElement element, Thickness margin)
    {
        element.Margin = margin;
        return margin;
    }
}
