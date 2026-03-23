//////////////////////////////////////////////
// Project      : WpfHexEditor.Commands
// File         : CommandRegistry.cs
// Description  : Thread-safe implementation of ICommandRegistry.
// Architecture : Singleton lifetime managed by MainWindow; injected into IIDEHostContext.
//////////////////////////////////////////////

namespace WpfHexEditor.Commands;

/// <summary>
/// Thread-safe central store for all IDE command definitions.
/// </summary>
public sealed class CommandRegistry : ICommandRegistry
{
    private readonly Dictionary<string, CommandDefinition> _commands =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly object _lock = new();

    /// <inheritdoc />
    public void Register(CommandDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        lock (_lock)
            _commands[definition.Id] = definition;
    }

    /// <inheritdoc />
    public void Unregister(string id)
    {
        lock (_lock)
            _commands.Remove(id);
    }

    /// <inheritdoc />
    public IReadOnlyList<CommandDefinition> GetAll()
    {
        lock (_lock)
        {
            return _commands.Values
                .OrderBy(c => c.Category, StringComparer.OrdinalIgnoreCase)
                .ThenBy(c => c.Name,     StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    /// <inheritdoc />
    public CommandDefinition? Find(string id)
    {
        lock (_lock)
            return _commands.TryGetValue(id, out var def) ? def : null;
    }
}
