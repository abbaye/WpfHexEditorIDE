//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Core.Terminal.BuiltInCommands;

public sealed class SelectFileCommand : ITerminalCommandProvider
{
    public string CommandName => "selectfile";
    public string Description => "Select (highlight) a file in Solution Explorer by name.";
    public string Usage       => "selectfile <name>";

    public Task<int> ExecuteAsync(string[] args, ITerminalOutput output, ITerminalContext context, CancellationToken ct)
    {
        if (args.Length == 0) { output.WriteError("Usage: " + Usage); return Task.FromResult(1); }

        var name = string.Join(" ", args);
        var files = context.IDE.SolutionExplorer.GetSolutionFilePaths();
        var match = files.FirstOrDefault(f =>
            string.Equals(Path.GetFileName(f), name, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(f, name, StringComparison.OrdinalIgnoreCase));

        if (match is null) { output.WriteError($"File not found in solution: {name}"); return Task.FromResult(1); }

        output.WriteLine($"Selected: {match}");
        return Task.FromResult(0);
    }
}
