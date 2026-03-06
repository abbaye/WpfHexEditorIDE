//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

using System.ComponentModel;
using System.Runtime.CompilerServices;
using WpfHexEditor.Docking.Core;

namespace WpfHexEditor.Docking.Core.Nodes;

/// <summary>
/// Represents an individual panel or document in the dock layout.
/// </summary>
public class DockItem : INotifyPropertyChanged
{
    private string _title = string.Empty;

    public Guid Id { get; } = Guid.NewGuid();

    public required string Title
    {
        get => _title;
        set { if (_title != value) { _title = value; OnPropertyChanged(); } }
    }

    /// <summary>
    /// Unique identifier used to match content when restoring layouts.
    /// </summary>
    public required string ContentId { get; set; }

    public bool CanClose { get; set; } = true;

    public bool CanFloat { get; set; } = true;

    /// <summary>
    /// Whether this document tab is pinned (moved to the left, protected from Close All).
    /// </summary>
    public bool IsPinned { get; set; }

    private bool _isSticky;

    /// <summary>
    /// When <c>true</c> the tab is never moved to the overflow menu —
    /// it remains permanently visible in the tab strip.
    /// Toggle via the "Pin Tab" context menu item.
    /// </summary>
    public bool IsSticky
    {
        get => _isSticky;
        set { if (_isSticky != value) { _isSticky = value; OnPropertyChanged(); } }
    }

    /// <summary>
    /// True if this item is a document (file content) rather than a tool panel.
    /// Set automatically by <see cref="DockGroupNode.AddItem"/> / <see cref="DockGroupNode.InsertItem"/>
    /// based on whether the container is a <see cref="DocumentHostNode"/>.
    /// Preserved while floating so the drag manager can route drops correctly.
    /// </summary>
    public bool IsDocument { get; set; }

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
    /// Optional icon displayed in tab headers and navigator. Typically an ImageSource,
    /// but can be any WPF content. Not serialized — set by the content factory at runtime.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public object? Icon { get; set; }

    /// <summary>
    /// Application-defined data associated with this item. Not serialized.
    /// </summary>
    public object? Tag { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
