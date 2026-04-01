// ==========================================================
// Project: WpfHexEditor.Core.Terminal
// File: ReplayHistoryCommand.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-08
// Description:
//     Built-in terminal command: replay-history [N]
//     Re-executes the last N commands from the active session history.
//     Omitting N replays the full history.
//
// Architecture Notes:
//     Feature #92: History replay.
//     Pattern: Command.
//     The history accessor is a Func<IEnumerable<string>> so the command
//     always reads the current session's history at execution time.
//
// ==========================================================

using WpfHexEditor.Core.Terminal.Macros;

namespace WpfHexEditor.Core.Terminal.BuiltInCommands;

/// <summary>
/// Built-in command: <c>replay-history [N]</c>
/// Re-executes the last N commands (or all) from the active session history.
/// </summary>
public sealed class ReplayHistoryCommand : ITerminalCommandProvider
{
    private readonly ITerminalMacroService _macroService;

    /// <summary>
    /// Delegate that returns the current session's history entries (most-recent first).
    /// Injected so the command always reads the live session state.
    /// </summary>
    private readonly Func<IEnumerable<string>> _historyProvider;

    public ReplayHistoryCommand(
        ITerminalMacroService macroService,
        Func<IEnumerable<string>> historyProvider)
    {
        _macroService    = macroService    ?? throw new ArgumentNullException(nameof(macroService));
        _historyProvider = historyProvider ?? throw new ArgumentNullException(nameof(historyProvider));
    }

    public string CommandName => "replay-history";
    public string Description => "Re-execute the last N commands from history.";
    public string Usage       => "replay-history [N]";

    public Task<int> ExecuteAsync(
        string[] args,
        ITerminalOutput output,
        ITerminalContext context,
        CancellationToken ct = default)
    {
        int count = int.MaxValue;

        if (args.Length > 0)
        {
            if (!int.TryParse(args[0], out count) || count <= 0)
            {
                output.WriteError($"Invalid count '{args[0]}'. Must be a positive integer.");
                return Task.FromResult(1);
            }
        }

        var history = _historyProvider();

        return _macroService.ReplayHistoryAsync(history, count, output, context, ct)
            .ContinueWith(_ => 0, TaskContinuationOptions.ExecuteSynchronously);
    }
}
