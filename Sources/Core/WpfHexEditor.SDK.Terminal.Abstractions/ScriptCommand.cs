// ==========================================================
// Project: WpfHexEditor.SDK
// File: Contracts/Terminal/ScriptCommand.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-30
// Description:
//     Lightweight ITerminalCommandProvider implementation for use in .csx scripts.
//     Wraps a delegate so script authors do not need to declare a full class.
//
// Architecture Notes:
//     Source defaults to "Script" so help output groups it separately from
//     built-in and plugin commands.
// ==========================================================

namespace WpfHexEditor.SDK.Contracts.Terminal;

/// <summary>
/// Delegate-based terminal command for use in .csx scripts.
/// Allows registering a command with a single expression:
/// <code>
/// Terminal?.RegisterCommand(new ScriptCommand(
///     name:    "my-cmd",
///     description: "Does something.",
///     usage:   "my-cmd [arg]",
///     execute: async (args, output, ctx, ct) => { output.WriteInfo("Hi!"); return 0; }));
/// </code>
/// </summary>
public sealed class ScriptCommand : ITerminalCommandProvider
{
    private readonly Func<string[], ITerminalOutput, ITerminalContext, CancellationToken, Task<int>> _execute;

    /// <param name="name">Command name (lowercase, no spaces).</param>
    /// <param name="description">Short description for <c>help</c> output.</param>
    /// <param name="usage">Usage syntax for <c>help &lt;name&gt;</c>.</param>
    /// <param name="execute">Async delegate implementing the command logic.</param>
    /// <param name="source">Origin label shown in <c>help</c> output. Defaults to <c>"Script"</c>.</param>
    public ScriptCommand(
        string name,
        string description,
        string usage,
        Func<string[], ITerminalOutput, ITerminalContext, CancellationToken, Task<int>> execute,
        string source = "Script")
    {
        CommandName = name        ?? throw new ArgumentNullException(nameof(name));
        Description = description ?? throw new ArgumentNullException(nameof(description));
        Usage       = usage       ?? throw new ArgumentNullException(nameof(usage));
        Source      = source;
        _execute    = execute     ?? throw new ArgumentNullException(nameof(execute));
    }

    /// <inheritdoc />
    public string  CommandName { get; }

    /// <inheritdoc />
    public string  Description { get; }

    /// <inheritdoc />
    public string  Usage { get; }

    /// <inheritdoc />
    public string? Source { get; }

    /// <inheritdoc />
    public async Task<int> ExecuteAsync(
        string[] args,
        ITerminalOutput output,
        ITerminalContext context,
        CancellationToken ct = default)
    {
        try
        {
            return await _execute(args, output, context, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            output.WriteWarning("[Cancelled]");
            return 1;
        }
        catch (Exception ex)
        {
            output.WriteError($"Error: {ex.Message}");
            return 1;
        }
    }
}
