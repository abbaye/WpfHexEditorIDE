//////////////////////////////////////////////
// Project      : WpfHexEditor.SDK
// File         : Commands/ICommandRegistry.cs
// Description  : Plugin-facing mirror of the IDE CommandRegistry.
//                Allows plugins to register custom commands that appear in the
//                Command Palette and Keyboard Shortcuts options page.
// Architecture : Added to IIDEHostContext as a DIM — zero breaking change for
//                plugins compiled against earlier SDK versions.
//////////////////////////////////////////////

using System.Windows.Input;

namespace WpfHexEditor.SDK.Commands;

/// <summary>
/// Describes a plugin-contributed IDE command.
/// </summary>
/// <param name="Id">Unique dot-separated identifier, e.g. <c>MyPlugin.DoSomething</c>.</param>
/// <param name="Name">Human-readable display name.</param>
/// <param name="Category">Grouping category shown in Command Palette and Keyboard Shortcuts.</param>
/// <param name="DefaultGesture">Optional default keyboard gesture (e.g. "Ctrl+Alt+X").</param>
/// <param name="IconGlyph">Optional Segoe MDL2 Assets character code.</param>
/// <param name="Command">ICommand to execute.</param>
public sealed record SdkCommandDefinition(
    string Id,
    string Name,
    string Category,
    string? DefaultGesture,
    string? IconGlyph,
    ICommand Command);

/// <summary>
/// Plugin-facing view of the IDE command registry.
/// Register commands on plugin load; unregister on plugin unload.
/// </summary>
public interface ICommandRegistry
{
    /// <summary>Registers a plugin command. Replaces any existing entry with the same Id.</summary>
    void Register(SdkCommandDefinition definition);

    /// <summary>Removes the plugin command with the given <paramref name="id"/>.</summary>
    void Unregister(string id);

    /// <summary>Returns all currently registered commands (built-in + plugin).</summary>
    IReadOnlyList<SdkCommandDefinition> GetAll();

    /// <summary>Finds a command by Id, or returns null.</summary>
    SdkCommandDefinition? Find(string id);
}
