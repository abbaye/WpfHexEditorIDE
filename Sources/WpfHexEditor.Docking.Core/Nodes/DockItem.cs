//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using WpfHexEditor.Docking.Core;

namespace WpfHexEditor.Docking.Core.Nodes;

/// <summary>
/// Represents an individual panel or document in the dock layout.
/// </summary>
public class DockItem
{
    public Guid Id { get; } = Guid.NewGuid();

    public required string Title { get; set; }

    /// <summary>
    /// Unique identifier used to match content when restoring layouts.
    /// </summary>
    public required string ContentId { get; set; }

    public bool CanClose { get; set; } = true;

    public bool CanFloat { get; set; } = true;

    public DockItemState State { get; set; } = DockItemState.Docked;

    /// <summary>
    /// Remembers which side this item was docked on before auto-hide.
    /// Used to place it in the correct auto-hide bar.
    /// </summary>
    public DockSide LastDockSide { get; set; } = DockSide.Bottom;

    /// <summary>
    /// The group this item belongs to (null if floating or hidden).
    /// </summary>
    public DockGroupNode? Owner { get; internal set; }

    /// <summary>
    /// Last known position of the floating window (null = not yet set / use default centering).
    /// Updated automatically by <see cref="FloatingWindowManager"/> as the window is moved.
    /// </summary>
    public double? FloatLeft { get; set; }

    /// <inheritdoc cref="FloatLeft"/>
    public double? FloatTop { get; set; }

    /// <summary>
    /// Last known width of the floating window (null = use default 400).
    /// Updated by <see cref="FloatingWindowManager"/> on <c>SizeChanged</c>.
    /// </summary>
    public double? FloatWidth { get; set; }

    /// <summary>
    /// Last known height of the floating window (null = use default 300).
    /// Updated by <see cref="FloatingWindowManager"/> on <c>SizeChanged</c>.
    /// </summary>
    public double? FloatHeight { get; set; }

    /// <summary>
    /// Application-defined key/value pairs that are serialized with the layout.
    /// Use this to persist custom data such as file paths across sessions.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = [];

    /// <summary>
    /// Application-defined data associated with this item. Not serialized.
    /// </summary>
    public object? Tag { get; set; }
}
