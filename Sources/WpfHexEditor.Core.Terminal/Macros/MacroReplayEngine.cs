// ==========================================================
// Project: WpfHexEditor.Core.Terminal
// File: MacroReplayEngine.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-08
// Description:
//     Replays MacroSession entries or a history batch through the
//     TerminalCommandRegistry, and exports sessions to .hxscript format.
//
// Architecture Notes:
//     Pattern: Interpreter (dispatches each RawCommand via the registry).
//     Feature #92: Script execution / macro replay / history replay.
//     Commands are tokenised identically to ExecuteInputAsync in the VM.
//     Cancellation is checked before each entry to allow mid-replay abort.
//
// ==========================================================

using System.Text;

namespace WpfHexEditor.Core.Terminal.Macros;

/// <summary>
/// Replays <see cref="MacroSession"/> commands and exports them as <c>.hxscript</c> source.
/// </summary>
public sealed class MacroReplayEngine
{
    private readonly TerminalCommandRegistry _registry;

    public MacroReplayEngine(TerminalCommandRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    // -- Replay -------------------------------------------------------------------

    /// <summary>
    /// Executes all entries of <paramref name="session"/> in order.
    /// Writes a header/footer to <paramref name="output"/> for visibility.
    /// </summary>
    public async Task ReplayAsync(
        MacroSession session,
        ITerminalOutput output,
        ITerminalContext context,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(output);

        if (session.IsEmpty)
        {
            output.WriteWarning("Macro replay: session is empty.");
            return;
        }

        output.WriteInfo($"[Macro replay: {session.Name} — {session.Entries.Count} command(s)]");

        await ExecuteCommandsAsync(session.Entries.Select(e => e.RawCommand), output, context, ct)
            .ConfigureAwait(false);

        output.WriteInfo("[Macro replay complete]");
    }

    /// <summary>
    /// Replays the last <paramref name="count"/> commands from <paramref name="commands"/>.
    /// </summary>
    public async Task ReplayHistoryAsync(
        IEnumerable<string> commands,
        int count,
        ITerminalOutput output,
        ITerminalContext context,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(output);

        var list = commands.TakeLast(count).ToList();

        if (list.Count == 0)
        {
            output.WriteWarning("replay-history: no commands in history.");
            return;
        }

        output.WriteInfo($"[History replay: {list.Count} command(s)]");

        await ExecuteCommandsAsync(list, output, context, ct).ConfigureAwait(false);

        output.WriteInfo("[History replay complete]");
    }

    // -- Export -------------------------------------------------------------------

    /// <summary>
    /// Converts a <see cref="MacroSession"/> to <c>.hxscript</c> source.
    /// Each entry is emitted as a plain command line (one per line).
    /// </summary>
    public string ExportToHxScript(MacroSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var sb = new StringBuilder();
        sb.AppendLine($"# Macro: {session.Name}");
        sb.AppendLine($"# Recorded: {session.StartedAt:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"# Commands: {session.Entries.Count}");
        sb.AppendLine();

        foreach (var entry in session.Entries)
        {
            sb.AppendLine($"# {entry.Timestamp:HH:mm:ss}");
            sb.AppendLine(entry.RawCommand);
        }

        return sb.ToString();
    }

    // -- Private ------------------------------------------------------------------

    private async Task ExecuteCommandsAsync(
        IEnumerable<string> rawCommands,
        ITerminalOutput output,
        ITerminalContext context,
        CancellationToken ct)
    {
        foreach (var raw in rawCommands)
        {
            ct.ThrowIfCancellationRequested();

            var parts = Tokenize(raw);
            if (parts.Length == 0) continue;

            var cmd = _registry.FindCommand(parts[0]);
            if (cmd is null)
            {
                output.WriteError($"replay: unknown command '{parts[0]}'");
                continue;
            }

            output.WriteInfo($"> {raw}");

            try
            {
                await cmd.ExecuteAsync(parts[1..], output, context, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                output.WriteWarning("[replay cancelled]");
                throw;
            }
            catch (Exception ex)
            {
                output.WriteError($"replay error: {ex.Message}");
            }
        }
    }

    private static string[] Tokenize(string input)
    {
        var args = new List<string>();
        bool inQuote = false;
        var current = new StringBuilder();

        foreach (var ch in input)
        {
            if (ch == '"') { inQuote = !inQuote; continue; }
            if (ch == ' ' && !inQuote)
            {
                if (current.Length > 0) { args.Add(current.ToString()); current.Clear(); }
                continue;
            }
            current.Append(ch);
        }

        if (current.Length > 0) args.Add(current.ToString());
        return [.. args];
    }
}
