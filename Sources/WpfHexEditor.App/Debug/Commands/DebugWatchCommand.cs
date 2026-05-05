// ==========================================================
// Project: WpfHexEditor.App.Debug
// File: Commands/DebugWatchCommand.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-30
// Description: HxTerminal command — show Watch panel and focus it.
// ==========================================================

using WpfHexEditor.SDK.Contracts.Terminal;

namespace WpfHexEditor.App.Debug.Commands;

internal sealed class DebugWatchCommand : PluginTerminalCommandBase
{
    public override string CommandName => "debug-watch";
    public override string Description => "Show the Watch panel. Optionally provide an expression hint.";
    public override string Usage       => "debug-watch [expression]";

    protected override Task<int> ExecuteCoreAsync(
        string[] args, ITerminalOutput output, ITerminalContext ctx, CancellationToken ct)
    {
        ctx.IDE().UIRegistry.ShowPanel("panel-dbg-watch");

        if (args.Length > 0)
            output.WriteInfo($"Watch panel opened — add expression: {string.Join(" ", args)}");
        else
            output.WriteInfo("Watch panel opened.");

        return Task.FromResult(0);
    }
}
