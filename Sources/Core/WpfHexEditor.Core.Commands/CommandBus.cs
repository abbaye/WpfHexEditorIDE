// ==========================================================
// Project: WpfHexEditor.Core.Commands
// File: CommandBus.cs
// Description:
//     Registry-backed implementation of ICommandBus. Looks up the
//     CommandDefinition by id, queries CanExecute, calls Execute, and
//     publishes a CommandInvokedNotification for every dispatch attempt.
// ==========================================================

namespace WpfHexEditor.Core.Commands;

/// <summary>Default <see cref="ICommandBus"/> — delegates to <see cref="ICommandRegistry"/>.</summary>
public sealed class CommandBus : ICommandBus
{
    private readonly ICommandRegistry _registry;

    public CommandBus(ICommandRegistry registry) => _registry = registry;

    public event EventHandler<CommandInvokedNotification>? CommandInvoked;

    public bool CanExecute(string commandId, object? parameter = null)
    {
        var def = _registry.Find(commandId);
        return def?.Command?.CanExecute(parameter) == true;
    }

    public CommandExecutionResult Execute(string commandId, object? parameter = null)
    {
        var def = _registry.Find(commandId);
        var timestamp = DateTime.UtcNow;

        if (def?.Command is null)
        {
            Notify(commandId, parameter, CommandExecutionResult.NotFound, null, timestamp);
            return CommandExecutionResult.NotFound;
        }

        if (!def.Command.CanExecute(parameter))
        {
            Notify(commandId, parameter, CommandExecutionResult.Disabled, null, timestamp);
            return CommandExecutionResult.Disabled;
        }

        try
        {
            def.Command.Execute(parameter);
            Notify(commandId, parameter, CommandExecutionResult.Executed, null, timestamp);
            return CommandExecutionResult.Executed;
        }
        catch (Exception ex)
        {
            Notify(commandId, parameter, CommandExecutionResult.Faulted, ex, timestamp);
            return CommandExecutionResult.Faulted;
        }
    }

    private void Notify(string id, object? param, CommandExecutionResult result, Exception? error, DateTime timestamp)
    {
        var handler = CommandInvoked;
        if (handler is null) return; // skip allocation when no subscribers
        handler(this, new CommandInvokedNotification(id, param, result, error, timestamp));
    }
}
