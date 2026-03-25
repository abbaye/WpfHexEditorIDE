// ==========================================================
// Project: WpfHexEditor.Core.Terminal
// File: BuiltInCommands/DebugContinueCommand.cs
// Description: Terminal command — resume paused debugger execution.
// ==========================================================

using WpfHexEditor.SDK.Contracts.Services;

namespace WpfHexEditor.Core.Terminal.BuiltInCommands;

/// <summary>
/// Terminal command: <c>debug-continue</c>
/// Resumes execution when the debugger is paused at a breakpoint.
/// </summary>
public sealed class DebugContinueCommand : ITerminalCommandProvider
{
    public string CommandName => "debug-continue";
    public string Description => "Resume execution when the debugger is paused.";
    public string Usage       => "debug-continue";

    public async Task<int> ExecuteAsync(string[] args, ITerminalOutput output,
        ITerminalContext context, CancellationToken ct)
    {
        var dbg = context.IDE.Debugger;
        if (dbg is null) { output.WriteError("Debugger not available."); return 1; }

        if (!dbg.IsPaused)
        {
            output.WriteWarning($"Cannot continue — debugger is {dbg.State} (must be Paused).");
            return 1;
        }

        await dbg.ContinueAsync().ConfigureAwait(false);
        output.WriteInfo("Execution resumed.");
        return 0;
    }
}
