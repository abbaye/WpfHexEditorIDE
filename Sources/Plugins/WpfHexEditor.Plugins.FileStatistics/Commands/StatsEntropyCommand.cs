// ==========================================================
// Project: WpfHexEditor.Plugins.FileStatistics
// File: Commands/StatsEntropyCommand.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-30
// Description: HxTerminal command — show entropy value and classification.
// ==========================================================

using WpfHexEditor.Plugins.FileStatistics.Views;
using WpfHexEditor.SDK.Contracts.Terminal;

namespace WpfHexEditor.Plugins.FileStatistics.Commands;

internal sealed class StatsEntropyCommand(Func<FileStats?> getStats) : PluginTerminalCommandBase
{
    public override string CommandName => "stats-entropy";
    public override string Description => "Show the Shannon entropy and data type classification.";
    public override string Usage       => "stats-entropy";

    protected override Task<int> ExecuteCoreAsync(
        string[] args, ITerminalOutput output, ITerminalContext ctx, CancellationToken ct)
    {
        var stats = getStats();

        if (stats is null)
        {
            output.WriteWarning("No statistics available. Open a file first.");
            return Task.FromResult(0);
        }

        var bar = BuildBar(stats.Entropy, 8.0, 20);

        output.WriteInfo($"Entropy: {stats.Entropy:F4} bits/byte  [{bar}]  → {stats.DataType}");
        return Task.FromResult(0);
    }

    private static string BuildBar(double value, double max, int width)
    {
        var filled = (int)Math.Round(value / max * width);
        return new string('█', filled) + new string('░', width - filled);
    }
}
