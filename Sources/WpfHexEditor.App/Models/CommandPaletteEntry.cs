//////////////////////////////////////////////
// Project      : WpfHexEditor.App
// File         : CommandPaletteEntry.cs
// Description  : Immutable entry representing a single command in the Command Palette.
//                No WPF dependency — safe for unit testing.
// Architecture : Domain record; consumed by CommandPaletteService and CommandPaletteWindow.
//////////////////////////////////////////////

using System.Windows.Input;

namespace WpfHexEditor.App.Models;

/// <summary>
/// Represents a single command exposed in the Command Palette (Ctrl+Shift+P).
/// </summary>
/// <param name="Name">Display name shown as the primary label.</param>
/// <param name="Category">Grouping category (e.g. "File", "Edit", "Plugins").</param>
/// <param name="GestureText">Optional keyboard shortcut text (e.g. "Ctrl+S").</param>
/// <param name="IconGlyph">Optional Segoe MDL2 Assets character (e.g. "\uE74E").</param>
/// <param name="Command">Command to execute; null entries are display-only.</param>
/// <param name="CommandParameter">Optional parameter forwarded to <see cref="Command"/>.</param>
/// <param name="Description">Optional human-readable description shown as tooltip or in the bottom panel.</param>
/// <param name="MatchIndices">Indices within <see cref="Name"/> that matched the search query (for highlight rendering).</param>
/// <param name="IsGroupHeader">When true this entry is a visual category separator, not an executable command.</param>
/// <param name="IsRecent">When true this entry was placed at the top because it was recently executed.</param>
public sealed record CommandPaletteEntry(
    string    Name,
    string    Category,
    string?   GestureText,
    string?   IconGlyph,
    ICommand? Command,
    object?   CommandParameter = null,
    string?   Description      = null,
    int[]?    MatchIndices      = null,
    bool      IsGroupHeader     = false,
    bool      IsRecent          = false);
