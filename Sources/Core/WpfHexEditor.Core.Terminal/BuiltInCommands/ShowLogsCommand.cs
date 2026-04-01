//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Core.Terminal.BuiltInCommands;

public sealed class ShowLogsCommand : ITerminalCommandProvider
{
    public string CommandName => "showlogs";
    public string Description => "Display the last N output panel log lines (default: 20).";
    public string Usage       => "showlogs [n]";

    public Task<int> ExecuteAsync(string[] args, ITerminalOutput output, ITerminalContext context, CancellationToken ct)
    {
        var count = 20;
        if (args.Length > 0 && int.TryParse(args[0], out var n) && n > 0) count = n;

        var lines = context.IDE.Output.GetRecentLines(count);
        if (lines.Count == 0) { output.WriteLine("(no log entries)"); return Task.FromResult(0); }

        foreach (var line in lines) output.WriteLine(line);
        output.WriteLine($"--- {lines.Count} line(s) ---");
        return Task.FromResult(0);
    }
}
