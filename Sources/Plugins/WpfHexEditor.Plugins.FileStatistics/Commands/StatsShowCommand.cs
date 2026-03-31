// ==========================================================
// Project: WpfHexEditor.Plugins.FileStatistics
// File: Commands/StatsShowCommand.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-30
// Description: HxTerminal command — show file statistics summary in the terminal.
// ==========================================================

using WpfHexEditor.Plugins.FileStatistics.Views;
using WpfHexEditor.SDK.Contracts.Terminal;

namespace WpfHexEditor.Plugins.FileStatistics.Commands;

internal sealed class StatsShowCommand(Func<FileStats?> getStats) : PluginTerminalCommandBase
{
    public override string CommandName => "stats-show";
    public override string Description => "Display file statistics for the currently open file.";
    public override string Usage       => "stats-show";

    protected override Task<int> ExecuteCoreAsync(
        string[] args, ITerminalOutput output, ITerminalContext ctx, CancellationToken ct)
    {
        var stats = getStats();

        if (stats is null)
        {
            output.WriteWarning("No statistics available. Open a file in the Hex Editor first.");
            ctx.IDE.UIRegistry.ShowPanel("WpfHexEditor.Plugins.FileStatistics.Panel.FileStatisticsPanel");
            return Task.FromResult(0);
        }

        output.WriteInfo($"File Statistics — {stats.FileName}");
        WriteRow(output, "File",        stats.FilePath ?? stats.FileName ?? "");
        WriteRow(output, "Size",        $"{stats.FileSize:N0} bytes");
        WriteRow(output, "Data type",   stats.DataType);
        WriteRow(output, "Entropy",     $"{stats.Entropy:F2} bits/byte");
        WriteRow(output, "Health",      $"{stats.HealthScore}/100  ({stats.HealthMessage})");
        WriteRow(output, "Null bytes",  $"{stats.NullBytePercentage:F1}%");
        WriteRow(output, "Printable",   $"{stats.PrintableAsciiPercentage:F1}%");

        if (stats.Anomalies.Count > 0)
        {
            output.WriteWarning($"Anomalies ({stats.Anomalies.Count}):");
            foreach (var a in stats.Anomalies)
                output.WriteWarning($"  ⚠ {a.Title}: {a.Description}");
        }

        return Task.FromResult(0);
    }
}
