//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Core.Terminal.BuiltInCommands;

public sealed class HistoryCommand(CommandHistory history) : ITerminalCommandProvider
{
    public string CommandName => "history";
    public string Description => "Show command history.";
    public string Usage       => "history [n]";

    public Task<int> ExecuteAsync(string[] args, ITerminalOutput output, ITerminalContext context, CancellationToken ct)
    {
        var all = history.GetAll();
        int limit = args.Length > 0 && int.TryParse(args[0], out var n) ? n : all.Count;
        int index = 1;
        foreach (var entry in all.Take(limit))
            output.WriteLine($"  {index++,4}  {entry}");
        return Task.FromResult(0);
    }
}
