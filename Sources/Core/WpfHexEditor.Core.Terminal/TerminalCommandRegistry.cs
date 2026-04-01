// ==========================================================
// Project: WpfHexEditor.Core.Terminal
// File: TerminalCommandRegistry.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03
// Description:
//     Thread-safe registry of all available terminal commands.
//     Commands are keyed by their lowercase CommandName.
//     Feature #92: Added CommandExecuted event for macro recording integration.
//
// Architecture Notes:
//     CommandExecuted is fired (outside the lock) after a command name is
//     successfully resolved, allowing MacroRecorder to subscribe without
//     coupling to any specific command implementation.
//
// ==========================================================

namespace WpfHexEditor.Core.Terminal;

/// <summary>
/// Thread-safe registry of all available terminal commands.
/// Commands are keyed by their lowercase <see cref="ITerminalCommandProvider.CommandName"/>.
/// </summary>
public sealed class TerminalCommandRegistry
{
    private readonly Dictionary<string, ITerminalCommandProvider> _commands
        = new(StringComparer.OrdinalIgnoreCase);

    private readonly object _lock = new();

    // -- Events -------------------------------------------------------------------

    /// <summary>
    /// Raised after a command is successfully resolved (not after execution completes).
    /// Argument is the raw command string as entered by the user.
    /// Used by <see cref="Macros.MacroRecorder"/> to capture executed commands.
    /// </summary>
    public event EventHandler<string>? CommandExecuted;

    // -- Registration -------------------------------------------------------------

    public void Register(ITerminalCommandProvider command)
    {
        ArgumentNullException.ThrowIfNull(command);
        lock (_lock) _commands[command.CommandName] = command;
    }

    public void Unregister(string commandName)
    {
        lock (_lock) _commands.Remove(commandName);
    }

    // -- Lookup -------------------------------------------------------------------

    public ITerminalCommandProvider? FindCommand(string commandName)
    {
        lock (_lock) return _commands.TryGetValue(commandName, out var cmd) ? cmd : null;
    }

    public IReadOnlyList<ITerminalCommandProvider> GetAll()
    {
        lock (_lock) return _commands.Values.OrderBy(c => c.CommandName).ToList();
    }

    /// <summary>
    /// Returns all command names that start with <paramref name="prefix"/>, sorted alphabetically.
    /// Used for Tab completion in the Terminal input box.
    /// </summary>
    public IReadOnlyList<string> GetCompletions(string prefix)
    {
        lock (_lock)
            return _commands.Keys
                .Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .OrderBy(k => k)
                .ToList();
    }

    // -- Macro recording hook -----------------------------------------------------

    /// <summary>
    /// Notifies macro observers that <paramref name="rawCommand"/> was executed.
    /// Must be called by the command dispatcher (TerminalPanelViewModel /
    /// ShellSessionViewModel) after successfully resolving a command — not inside
    /// the lock so that event handlers can call back into the registry safely.
    /// </summary>
    public void RaiseCommandExecuted(string rawCommand)
        => CommandExecuted?.Invoke(this, rawCommand);
}
