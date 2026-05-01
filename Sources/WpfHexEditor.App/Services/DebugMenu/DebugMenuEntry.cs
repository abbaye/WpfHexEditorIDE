//////////////////////////////////////////////
// Project      : WpfHexEditor.App
// File         : DebugMenuEntry.cs
// Description  : Unified model representing a single Debug menu item,
//                regardless of whether it is built-in or plugin-contributed.
// Architecture : Pure data record — no WPF dependencies.
//////////////////////////////////////////////

using System.Windows.Input;

namespace WpfHexEditor.App.Services.DebugMenu;

/// <summary>
/// Unified representation of a Debug menu item.
/// Merges data from built-in command registrations and plugin <c>MenuItemDescriptor</c>s.
/// </summary>
public sealed record DebugMenuEntry(
    /// <summary>Unique identifier — CommandId for built-in items, uiId for plugin items.</summary>
    string Id,

    /// <summary>Display text with optional access key (underscore prefix).</summary>
    string Header,

    /// <summary>Keyboard shortcut text (e.g. "F5"), or null.</summary>
    string? GestureText,

    /// <summary>Segoe MDL2 Assets glyph character, or null.</summary>
    string? IconGlyph,

    /// <summary>Command to execute when the item is clicked.</summary>
    ICommand? Command,

    /// <summary>Optional parameter passed to <see cref="Command"/>.</summary>
    object? CommandParameter,

    /// <summary>Logical group: "Session", "Stepping", "Breakpoints", or "Panels".</summary>
    string Group,

    /// <summary>Tooltip text, or null.</summary>
    string? ToolTip,

    /// <summary>True for hardcoded IDE items, false for plugin-contributed items.</summary>
    bool IsBuiltIn,

    /// <summary>
    /// Optional WPF resource key resolved via <c>Application.Current.TryFindResource</c>.
    /// When set, the resolved string is used as <see cref="Header"/> at menu-build time,
    /// falling back to <see cref="Header"/> if the key is missing.
    /// </summary>
    string? HeaderResourceKey = null
);
