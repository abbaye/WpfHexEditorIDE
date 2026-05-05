// ==========================================================
// Project: WpfHexEditor.App.Debug
// File: Commands/DebugBpSetCommand.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-30
// Description: HxTerminal command — set a breakpoint at a file/line with optional condition.
// ==========================================================

using WpfHexEditor.Core.Debugger.Models;
using WpfHexEditor.SDK.Contracts.Services;
using WpfHexEditor.SDK.Contracts.Terminal;

namespace WpfHexEditor.App.Debug.Commands;

internal sealed class DebugBpSetCommand(IDebuggerService debugger) : PluginTerminalCommandBase
{
    public override string CommandName => "debug-bp-set";
    public override string Description => "Set a breakpoint at <file>:<line> with an optional condition.";
    public override string Usage       => "debug-bp-set <file> <line> [condition]";

    protected override async Task<int> ExecuteCoreAsync(
        string[] args, ITerminalOutput output, ITerminalContext ctx, CancellationToken ct)
    {
        if (!RequireArgs(2, args, output, Usage)) return 1;

        var  filePath  = args[0];
        if (!int.TryParse(args[1], out var line) || line < 1)
        {
            output.WriteError($"Invalid line number: {args[1]}");
            return 1;
        }

        var condition = args.Length >= 3 ? string.Join(" ", args[2..]) : string.Empty;

        var settings = new BreakpointSettings(
            ConditionKind: string.IsNullOrEmpty(condition)
                ? BpConditionKind.None
                : BpConditionKind.ConditionalExpression,
            ConditionExpr: string.IsNullOrEmpty(condition) ? null : condition);

        await debugger.UpdateBreakpointSettingsAsync(filePath, line, settings).ConfigureAwait(false);

        output.WriteInfo($"Breakpoint set: {System.IO.Path.GetFileName(filePath)}:{line}" +
                         (string.IsNullOrEmpty(condition) ? "" : $" when {condition}"));
        return 0;
    }
}
