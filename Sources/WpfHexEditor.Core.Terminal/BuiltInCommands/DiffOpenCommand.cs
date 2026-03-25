// ==========================================================
// Project: WpfHexEditor.Core.Terminal
// File: BuiltInCommands/DiffOpenCommand.cs
// Description: Terminal command — open two files in the Diff Hub viewer panel.
// ==========================================================

namespace WpfHexEditor.Core.Terminal.BuiltInCommands;

/// <summary>
/// Terminal command: <c>diff-open &lt;file1&gt; &lt;file2&gt;</c>
/// Opens both files in the DiffHubPanel so the user can inspect changes visually.
/// </summary>
public sealed class DiffOpenCommand : ITerminalCommandProvider
{
    public string CommandName => "diff-open";
    public string Description => "Open two files in the Diff Hub viewer panel.";
    public string Usage       => "diff-open <file1> <file2>";

    public async Task<int> ExecuteAsync(string[] args, ITerminalOutput output,
        ITerminalContext context, CancellationToken ct)
    {
        if (args.Length < 2) { output.WriteError("Usage: " + Usage); return 1; }

        var svc = context.IDE.DiffService;
        if (svc is null) { output.WriteError("Diff service not available (FileComparison plugin not loaded)."); return 1; }

        var left  = args[0];
        var right = args[1];

        output.WriteInfo($"Opening Diff Hub: '{left}' ↔ '{right}'…");
        await svc.OpenInViewerAsync(left, right, ct).ConfigureAwait(false);
        return 0;
    }
}
