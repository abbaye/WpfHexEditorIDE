//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

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

    public void Register(ITerminalCommandProvider command)
    {
        ArgumentNullException.ThrowIfNull(command);
        lock (_lock) _commands[command.CommandName] = command;
    }

    public void Unregister(string commandName)
    {
        lock (_lock) _commands.Remove(commandName);
    }

    public ITerminalCommandProvider? FindCommand(string commandName)
    {
        lock (_lock) return _commands.TryGetValue(commandName, out var cmd) ? cmd : null;
    }

    public IReadOnlyList<ITerminalCommandProvider> GetAll()
    {
        lock (_lock) return _commands.Values.OrderBy(c => c.CommandName).ToList();
    }
}
