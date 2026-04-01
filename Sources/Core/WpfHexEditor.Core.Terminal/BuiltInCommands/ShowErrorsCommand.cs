//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Core.Terminal.BuiltInCommands;

public sealed class ShowErrorsCommand : ITerminalCommandProvider
{
    public string CommandName => "showerrors";
    public string Description => "Display the last N error panel entries (default: 20).";
    public string Usage       => "showerrors [n]";

    public Task<int> ExecuteAsync(string[] args, ITerminalOutput output, ITerminalContext context, CancellationToken ct)
    {
        var count = 20;
        if (args.Length > 0 && int.TryParse(args[0], out var n) && n > 0) count = n;

        var errors = context.IDE.ErrorPanel.GetRecentErrors(count);
        if (errors.Count == 0) { output.WriteLine("(no error entries)"); return Task.FromResult(0); }

        foreach (var e in errors) output.WriteError(e);
        output.WriteLine($"--- {errors.Count} error(s) ---");
        return Task.FromResult(0);
    }
}
