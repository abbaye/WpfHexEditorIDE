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
        if (def?.Command is null)
            return Notify(commandId, parameter, CommandExecutionResult.NotFound, null);

        if (!def.Command.CanExecute(parameter))
            return Notify(commandId, parameter, CommandExecutionResult.Disabled, null);

        try
        {
            def.Command.Execute(parameter);
            return Notify(commandId, parameter, CommandExecutionResult.Executed, null);
        }
        catch (Exception ex)
        {
            return Notify(commandId, parameter, CommandExecutionResult.Faulted, ex);
        }
    }

    private CommandExecutionResult Notify(string id, object? param, CommandExecutionResult result, Exception? error)
    {
        CommandInvoked?.Invoke(this, new CommandInvokedNotification(id, param, result, error, DateTime.UtcNow));
        return result;
    }
}
