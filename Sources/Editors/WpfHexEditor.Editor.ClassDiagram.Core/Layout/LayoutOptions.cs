// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: Layout/LayoutOptions.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-19
// Description:
//     Configuration record for the AutoLayoutEngine controlling spacing,
//     padding, minimum box dimensions, and per-member row height used
//     when computing diagram node positions and sizes.
//
// Architecture Notes:
//     Immutable class with init-only setters — callers create instances
//     via object-initializer syntax and pass them to AutoLayoutEngine.
//     All values are in logical pixels (device-independent units).
// ==========================================================

namespace WpfHexEditor.Editor.ClassDiagram.Core.Layout;

/// <summary>
/// Controls the sizing and spacing parameters used by <see cref="AutoLayoutEngine"/>.
/// All values are in logical (device-independent) pixels.
/// </summary>
public sealed class LayoutOptions
{
    /// <summary>Horizontal gap between columns of nodes. Default 60.</summary>
    public double ColSpacing { get; init; } = 60;

    /// <summary>Vertical gap between rows (layers) of nodes. Default 80.</summary>
    public double RowSpacing { get; init; } = 80;

    /// <summary>Padding around the entire diagram canvas. Default 40.</summary>
    public double CanvasPadding { get; init; } = 40;

    /// <summary>Minimum rendered width of any node box. Default 160.</summary>
    public double MinBoxWidth { get; init; } = 160;

    /// <summary>
    /// Horizontal padding added on each side of the widest member label
    /// when computing box width. Default 14.
    /// </summary>
    public double BoxPaddingH { get; init; } = 14;

    /// <summary>
    /// Vertical padding added above and below each member row.
    /// Default 6.
    /// </summary>
    public double BoxPaddingV { get; init; } = 6;

    /// <summary>Fixed height of the class name header row. Default 38.</summary>
    public double HeaderHeight { get; init; } = 38;

    /// <summary>Height of a single member row inside the box. Default 18.</summary>
    public double MemberHeight { get; init; } = 18;
}
