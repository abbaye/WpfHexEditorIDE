//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Core.Terminal.BuiltInCommands;

public sealed class OpenFolderCommand : ITerminalCommandProvider
{
    public string CommandName => "openfolder";
    public string Description => "Open a folder in Solution Explorer.";
    public string Usage       => "openfolder <path>";

    public async Task<int> ExecuteAsync(string[] args, ITerminalOutput output, ITerminalContext context, CancellationToken ct)
    {
        if (args.Length == 0) { output.WriteError("Usage: " + Usage); return 1; }
        var path = Path.IsPathRooted(args[0]) ? args[0] : Path.Combine(context.WorkingDirectory, args[0]);
        if (!Directory.Exists(path)) { output.WriteError($"Directory not found: {path}"); return 1; }
        await context.IDE.SolutionExplorer.OpenFolderAsync(path, ct).ConfigureAwait(false);
        output.WriteLine($"Opened folder: {path}");
        return 0;
    }
}
