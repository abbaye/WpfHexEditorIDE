// ==========================================================
// Project: WpfHexEditor.Core.Commands
// File: ICommandBus.cs
// Description:
//     Global command-execution facade. Routes by command id through the
//     CommandRegistry's CommandDefinition, supports parameterized invokes,
//     and publishes lifecycle events for telemetry / auditing.
// Architecture: registry-backed dispatcher — no execution state; callers
//                 stay decoupled from the underlying ICommand instance.
// ==========================================================

namespace WpfHexEditor.Core.Commands;

/// <summary>Outcome of a <see cref="ICommandBus"/> invocation.</summary>
public enum CommandExecutionResult
{
    /// <summary>Command was found, CanExecute was true, and Execute returned without throwing.</summary>
    Executed,
    /// <summary>No command with the given id is registered.</summary>
    NotFound,
    /// <summary>The command's <c>CanExecute</c> returned false.</summary>
    Disabled,
    /// <summary>The command threw an exception while executing.</summary>
    Faulted,
}

/// <summary>Lifecycle event raised on every <see cref="ICommandBus"/> dispatch.</summary>
public sealed record CommandInvokedNotification(
    string                  CommandId,
    object?                 Parameter,
    CommandExecutionResult  Result,
    Exception?              Error,
    DateTime                Timestamp);

/// <summary>
/// Global command-execution bus. Plugins, menus, toolbar, terminal, and
/// scripts all dispatch by id through this interface — they never call
/// the underlying <c>ICommand</c> directly.
/// </summary>
public interface ICommandBus
{
    /// <summary>Returns true if a command with the given id is registered and currently enabled.</summary>
    bool CanExecute(string commandId, object? parameter = null);

    /// <summary>Executes a registered command by id. Returns the dispatch outcome.</summary>
    CommandExecutionResult Execute(string commandId, object? parameter = null);

    /// <summary>Raised after every dispatch attempt — even when the command was not found.</summary>
    event EventHandler<CommandInvokedNotification>? CommandInvoked;
}
