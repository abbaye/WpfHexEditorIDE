// ==========================================================
// Project: WpfHexEditor.App.Debug
// File: Commands/DebugBpClearCommand.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-30
// Description: HxTerminal command — clear all breakpoints.
// ==========================================================

using WpfHexEditor.SDK.Contracts.Services;
using WpfHexEditor.SDK.Contracts.Terminal;

namespace WpfHexEditor.App.Debug.Commands;

internal sealed class DebugBpClearCommand(IDebuggerService debugger) : PluginTerminalCommandBase
{
    public override string CommandName => "debug-bp-clear";
    public override string Description => "Clear (delete) all breakpoints.";
    public override string Usage       => "debug-bp-clear";

    protected override async Task<int> ExecuteCoreAsync(
        string[] args, ITerminalOutput output, ITerminalContext ctx, CancellationToken ct)
    {
        var count = debugger.Breakpoints.Count;
        await debugger.ClearAllBreakpointsAsync().ConfigureAwait(false);
        output.WriteInfo($"Cleared {count} breakpoint{(count == 1 ? "" : "s")}.");
        return 0;
    }
}
