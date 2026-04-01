// ==========================================================
// Project: WpfHexEditor.Core.Terminal
// File: BuiltInCommands/DiffCommand.cs
// Description: Terminal command — compare two files and print a summary.
// ==========================================================

namespace WpfHexEditor.Core.Terminal.BuiltInCommands;

/// <summary>
/// Terminal command: <c>diff &lt;file1&gt; &lt;file2&gt;</c>
/// Compares two files and prints modified / added / removed counts and similarity %.
/// </summary>
public sealed class DiffCommand : ITerminalCommandProvider
{
    public string CommandName => "diff";
    public string Description => "Compare two files and print a change summary.";
    public string Usage       => "diff <file1> <file2>";

    public async Task<int> ExecuteAsync(string[] args, ITerminalOutput output,
        ITerminalContext context, CancellationToken ct)
    {
        if (args.Length < 2) { output.WriteError("Usage: " + Usage); return 1; }

        var svc = context.IDE.DiffService;
        if (svc is null) { output.WriteError("Diff service not available (FileComparison plugin not loaded)."); return 1; }

        var left  = args[0];
        var right = args[1];

        output.WriteInfo($"Comparing '{left}' ↔ '{right}'…");
        var summary = await svc.CompareAsync(left, right, ct).ConfigureAwait(false);

        output.WriteInfo(
            $"  Modified: {summary.Modified}  Added: {summary.Added}  Removed: {summary.Removed}" +
            $"  ({summary.TotalChanges} change(s), {summary.SimilarityPercent}% similar)");

        return summary.TotalChanges > 0 ? 1 : 0;
    }
}
