//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Core.Terminal.BuiltInCommands;

public sealed class OpenSolutionCommand : ITerminalCommandProvider
{
    public string CommandName => "opensolution";
    public string Description => "Open a solution file in the IDE.";
    public string Usage       => "opensolution <path>";

    public async Task<int> ExecuteAsync(string[] args, ITerminalOutput output, ITerminalContext context, CancellationToken ct)
    {
        if (args.Length == 0) { output.WriteError("Usage: " + Usage); return 1; }
        var path = Path.IsPathRooted(args[0]) ? args[0] : Path.Combine(context.WorkingDirectory, args[0]);
        if (!File.Exists(path)) { output.WriteError($"Solution file not found: {path}"); return 1; }
        await context.IDE.SolutionExplorer.OpenSolutionAsync(path, ct).ConfigureAwait(false);
        output.WriteLine($"Opening solution: {path}");
        return 0;
    }
}
