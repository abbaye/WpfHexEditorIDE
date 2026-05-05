// ==========================================================
// Project: WpfHexEditor.App.Debug
// File: Commands/DebugBpListCommand.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-30
// Description: HxTerminal command — list all breakpoints.
// ==========================================================

using WpfHexEditor.SDK.Contracts.Services;
using WpfHexEditor.SDK.Contracts.Terminal;

namespace WpfHexEditor.App.Debug.Commands;

internal sealed class DebugBpListCommand(IDebuggerService debugger) : PluginTerminalCommandBase
{
    public override string CommandName => "debug-bp-list";
    public override string Description => "List all breakpoints with file, line, and condition.";
    public override string Usage       => "debug-bp-list";

    protected override Task<int> ExecuteCoreAsync(
        string[] args, ITerminalOutput output, ITerminalContext ctx, CancellationToken ct)
    {
        var bps = debugger.Breakpoints;

        if (bps.Count == 0)
        {
            output.WriteWarning("No breakpoints set.");
            return Task.FromResult(0);
        }

        output.WriteInfo($"{bps.Count} breakpoint{(bps.Count == 1 ? "" : "s")}:");
        output.WriteLine($"  {"#",-4} {"Enabled",-8} {"File",-30} {"Line",-6} Condition");
        output.WriteLine($"  {"─",-4} {"───────",-8} {"────",-30} {"────",-6} ─────────");

        for (int i = 0; i < bps.Count; i++)
        {
            var bp      = bps[i];
            var enabled = bp.IsEnabled ? "✓" : "✗";
            var file    = System.IO.Path.GetFileName(bp.FilePath);
            var cond    = string.IsNullOrEmpty(bp.Condition) ? "" : bp.Condition;

            output.WriteLine($"  {i + 1,-4} {enabled,-8} {file,-30} {bp.Line,-6} {cond}");
        }

        return Task.FromResult(0);
    }
}
