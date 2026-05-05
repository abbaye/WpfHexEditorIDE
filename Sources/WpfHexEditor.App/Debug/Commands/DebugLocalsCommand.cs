// ==========================================================
// Project: WpfHexEditor.App.Debug
// File: Commands/DebugLocalsCommand.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-30
// Description: HxTerminal command — list local variables of the current stack frame.
// ==========================================================

using WpfHexEditor.SDK.Contracts.Services;
using WpfHexEditor.SDK.Contracts.Terminal;

namespace WpfHexEditor.App.Debug.Commands;

internal sealed class DebugLocalsCommand(IDebuggerService debugger) : PluginTerminalCommandBase
{
    public override string CommandName => "debug-locals";
    public override string Description => "Show local variables of the topmost (frame 0) stack frame.";
    public override string Usage       => "debug-locals";

    protected override async Task<int> ExecuteCoreAsync(
        string[] args, ITerminalOutput output, ITerminalContext ctx, CancellationToken ct)
    {
        var variables = await debugger.GetVariablesAsync(0).ConfigureAwait(false);

        if (variables.Count == 0)
        {
            output.WriteWarning("No locals available. Is the debugger paused at a breakpoint?");
            return 0;
        }

        output.WriteInfo($"Locals ({variables.Count}):");
        output.WriteLine($"  {"Name",-24} {"Type",-20} Value");
        output.WriteLine($"  {"────",-24} {"────",-20} ─────");

        foreach (var v in variables)
            output.WriteLine($"  {v.Name,-24} {v.Type,-20} {v.Value}");

        return 0;
    }
}
