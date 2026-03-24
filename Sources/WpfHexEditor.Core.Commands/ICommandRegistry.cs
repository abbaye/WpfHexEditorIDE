//////////////////////////////////////////////
// Project      : WpfHexEditor.Commands
// File         : ICommandRegistry.cs
// Description  : Central registry for all IDE commands (built-in + plugin-contributed).
// Architecture : Consumed by CommandPalette, KeyboardShortcutsPage, and MainWindow.
//                Exposed to plugins via IIDEHostContext.CommandRegistry (SDK mirror).
//////////////////////////////////////////////

namespace WpfHexEditor.Core.Commands;

/// <summary>
/// Central registry for all IDE commands.
/// Built-in commands are registered at startup; plugins register on load and unregister on unload.
/// </summary>
public interface ICommandRegistry
{
    /// <summary>Registers a command. Silently replaces any existing entry with the same <paramref name="definition"/> Id.</summary>
    void Register(CommandDefinition definition);

    /// <summary>Removes the command with the given <paramref name="id"/>. No-op if not found.</summary>
    void Unregister(string id);

    /// <summary>Returns a snapshot of all currently registered commands, sorted by Category then Name.</summary>
    IReadOnlyList<CommandDefinition> GetAll();

    /// <summary>Returns the definition with the given <paramref name="id"/>, or null if not registered.</summary>
    CommandDefinition? Find(string id);
}
