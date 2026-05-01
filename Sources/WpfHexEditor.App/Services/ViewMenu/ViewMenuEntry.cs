//////////////////////////////////////////////
// Project      : WpfHexEditor.App
// File         : ViewMenuEntry.cs
// Description  : Unified model representing a single View menu item,
//                regardless of whether it is built-in or plugin-contributed.
// Architecture : Pure data record — no WPF dependencies.
//////////////////////////////////////////////

using System.Windows.Input;

namespace WpfHexEditor.App.Services.ViewMenu;

/// <summary>
/// Unified representation of a View menu item.
/// Merges data from built-in command registrations and plugin <c>MenuItemDescriptor</c>s.
/// </summary>
public sealed record ViewMenuEntry(
    /// <summary>Unique identifier — CommandId for built-in items, uiId for plugin items.</summary>
    string Id,

    /// <summary>Display text with optional access key (underscore prefix).</summary>
    string Header,

    /// <summary>Keyboard shortcut text (e.g. "Ctrl+Alt+L"), or null.</summary>
    string? GestureText,

    /// <summary>Segoe MDL2 Assets glyph character, or null.</summary>
    string? IconGlyph,

    /// <summary>Command to execute when the item is clicked.</summary>
    ICommand? Command,

    /// <summary>Optional parameter passed to <see cref="Command"/>.</summary>
    object? CommandParameter,

    /// <summary>Original SDK <c>MenuItemDescriptor.Group</c> value (may be null).</summary>
    string? Group,

    /// <summary>
    /// Explicit category from SDK or auto-classified.
    /// Null means "not yet classified" — the classifier will assign one.
    /// </summary>
    string? Category,

    /// <summary>Default dock side from <c>PanelDescriptor</c> ("Left", "Right", "Bottom", "Center"), or null.</summary>
    string? DockSide,

    /// <summary>Tooltip text, or null.</summary>
    string? ToolTip,

    /// <summary>True for hardcoded IDE items, false for plugin-contributed items.</summary>
    bool IsBuiltIn,

    /// <summary>
    /// Optional WPF resource key resolved via <c>Application.Current.TryFindResource</c>.
    /// When set, overrides <see cref="Header"/> at menu-build time, falling back to
    /// <see cref="Header"/> if the key is missing.
    /// </summary>
    string? HeaderResourceKey = null,

    /// <summary>
    /// Optional WPF resource key for the tooltip, resolved via <c>Application.Current.TryFindResource</c>.
    /// When set, overrides <see cref="ToolTip"/> at menu-build time.
    /// </summary>
    string? ToolTipResourceKey = null
);
