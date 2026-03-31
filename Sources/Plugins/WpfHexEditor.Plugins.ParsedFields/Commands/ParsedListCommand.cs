// ==========================================================
// Project: WpfHexEditor.Plugins.ParsedFields
// File: Commands/ParsedListCommand.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-30
// Description: HxTerminal command — list parsed fields for the active file.
// ==========================================================

using WpfHexEditor.Core.ViewModels;
using WpfHexEditor.SDK.Contracts.Terminal;

namespace WpfHexEditor.Plugins.ParsedFields.Commands;

internal sealed class ParsedListCommand : PluginTerminalCommandBase
{
    public override string CommandName => "parsed-list";
    public override string Description => "List all parsed fields for the currently active file.";
    public override string Usage       => "parsed-list";

    protected override Task<int> ExecuteCoreAsync(
        string[] args, ITerminalOutput output, ITerminalContext ctx, CancellationToken ct)
    {
        var svc = ctx.IDE.FormatParsing;

        if (svc is null || !svc.HasParsedFields)
        {
            output.WriteWarning("No parsed fields available. Open a file with a known format first.");
            return Task.FromResult(0);
        }

        var fields = svc.GetParsedFields();
        output.WriteInfo($"{fields.Count} parsed field{(fields.Count == 1 ? "" : "s")}:");
        output.WriteLine($"  {"Offset",-12} {"Length",-8} {"Type",-16} {"Name",-24} Value");
        output.WriteLine($"  {"──────",-12} {"──────",-8} {"────",-16} {"────",-24} ─────");

        foreach (var f in fields)
            output.WriteLine($"  0x{f.Offset:X8}   {f.Length,-8} {f.ValueType,-16} {f.Name,-24} {f.FormattedValue}");

        return Task.FromResult(0);
    }
}
