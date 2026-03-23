//////////////////////////////////////////////
// Project      : WpfHexEditor.Commands
// File         : CommandDefinition.cs
// Description  : Immutable descriptor for a single IDE command.
//                Registered in CommandRegistry; surfaced by Command Palette.
// Architecture : Pure record — no WPF UI dependency, safe to use from any layer.
//////////////////////////////////////////////

using System.Windows.Input;

namespace WpfHexEditor.Commands;

/// <summary>
/// Describes a single IDE command: its identity, display metadata, default gesture,
/// and the <see cref="ICommand"/> delegate that executes it.
/// </summary>
/// <param name="Id">Unique dot-separated identifier, e.g. <c>File.Save</c>.</param>
/// <param name="Name">Human-readable display name shown in Command Palette and Keyboard Shortcuts page.</param>
/// <param name="Category">Grouping category (e.g. "File", "Edit", "View").</param>
/// <param name="DefaultGesture">Factory-default keyboard gesture string (e.g. "Ctrl+S"). Null = no default.</param>
/// <param name="IconGlyph">Optional Segoe MDL2 Assets character code.</param>
/// <param name="Command">WPF ICommand used to execute and canExecute-check this command.</param>
public sealed record CommandDefinition(
    string Id,
    string Name,
    string Category,
    string? DefaultGesture,
    string? IconGlyph,
    ICommand Command);
