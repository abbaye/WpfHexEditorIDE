// ==========================================================
// Project: WpfHexEditor.Core.Terminal
// File: BuiltInCommands/DebugStatusCommand.cs
// Description: Terminal command — show the current debugger session state.
// ==========================================================

namespace WpfHexEditor.Core.Terminal.BuiltInCommands;

/// <summary>
/// Terminal command: <c>debug-status</c>
/// Shows the debugger session state, paused location, and registered breakpoints.
/// </summary>
public sealed class DebugStatusCommand : ITerminalCommandProvider
{
    public string CommandName => "debug-status";
    public string Description => "Show the current debugger session state and registered breakpoints.";
    public string Usage       => "debug-status";

    public Task<int> ExecuteAsync(string[] args, ITerminalOutput output,
        ITerminalContext context, CancellationToken ct)
    {
        var dbg = context.IDE.Debugger;
        if (dbg is null)
        {
            output.WriteWarning("Debugger not available (plugin not loaded or no active session).");
            return Task.FromResult(0);
        }

        output.WriteInfo($"Debug state   : {dbg.State}");
        output.WriteInfo($"Session active: {dbg.IsActive}");

        if (dbg.IsPaused && dbg.PausedFilePath is not null)
            output.WriteWarning($"Paused at     : {System.IO.Path.GetFileName(dbg.PausedFilePath)} line {dbg.PausedLine}");

        var bp = dbg.Breakpoints;
        output.WriteInfo($"Breakpoints   : {bp.Count}");
        foreach (var b in bp)
        {
            var state = b.IsEnabled ? (b.IsVerified ? "✓" : "?") : "×";
            output.WriteLine($"  [{state}] {System.IO.Path.GetFileName(b.FilePath)}:{b.Line}" +
                             (b.Condition is not null ? $"  if {b.Condition}" : string.Empty));
        }

        return Task.FromResult(0);
    }
}
