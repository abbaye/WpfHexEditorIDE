// ==========================================================
// Project: WpfHexEditor.SDK
// File: Contracts/Terminal/PluginTerminalCommandBase.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-30
// Description:
//     Abstract base class for plugin-contributed terminal commands.
//     Provides uniform exception handling, cancellation forwarding,
//     argument validation helpers, and output formatting helpers so
//     plugin authors implement only ExecuteCoreAsync.
//
// Architecture Notes:
//     ExecuteAsync is sealed — plugins override ExecuteCoreAsync.
//     Cancellation and unhandled exceptions are caught centrally so
//     a buggy plugin command cannot crash the terminal dispatcher.
//     Implements ITerminalCommandProvider — fully compatible with
//     ITerminalService.RegisterCommand.
// ==========================================================

namespace WpfHexEditor.SDK.Contracts.Terminal;

/// <summary>
/// Abstract base for plugin-contributed HxTerminal commands.
/// Override <see cref="ExecuteCoreAsync"/> to implement the command logic.
/// </summary>
public abstract class PluginTerminalCommandBase : ITerminalCommandProvider
{
    /// <inheritdoc />
    public abstract string CommandName { get; }

    /// <inheritdoc />
    public abstract string Description { get; }

    /// <inheritdoc />
    public abstract string Usage { get; }

    /// <inheritdoc />
    /// <remarks>Returns <c>"Plugin"</c> for all plugin-contributed commands.
    /// Override to return a more specific label (e.g. the plugin display name).</remarks>
    public virtual string? Source => "Plugin";

    /// <summary>
    /// Sealed dispatcher: wraps <see cref="ExecuteCoreAsync"/> with
    /// uniform cancellation and exception handling.
    /// </summary>
    public async Task<int> ExecuteAsync(
        string[] args,
        ITerminalOutput output,
        ITerminalContext context,
        CancellationToken ct = default)
    {
        try
        {
            return await ExecuteCoreAsync(args, output, context, ct).ConfigureAwait(false);
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

    /// <summary>
    /// Implement the command logic here. Called by <see cref="ExecuteAsync"/>.
    /// Return 0 for success, non-zero for failure.
    /// </summary>
    protected abstract Task<int> ExecuteCoreAsync(
        string[] args,
        ITerminalOutput output,
        ITerminalContext context,
        CancellationToken ct);

    // ── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Validates that at least <paramref name="min"/> arguments were provided.
    /// Writes the usage string to output and returns false when the check fails.
    /// </summary>
    protected static bool RequireArgs(int min, string[] args, ITerminalOutput output, string usage)
    {
        if (args.Length >= min) return true;
        output.WriteError($"Usage: {usage}");
        return false;
    }

    /// <summary>
    /// Writes a two-column row with a left-aligned label and a value.
    /// Example: "  entropy               7.93 bits/byte"
    /// </summary>
    protected static void WriteRow(ITerminalOutput output, string label, string value)
        => output.WriteLine($"  {label,-22} {value}");
}
