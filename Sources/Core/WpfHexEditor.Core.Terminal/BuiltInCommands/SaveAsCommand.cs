//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Core.Terminal.BuiltInCommands;

public sealed class SaveAsCommand : ITerminalCommandProvider
{
    public string CommandName => "saveas";
    public string Description => "Save the active file under a new path.";
    public string Usage       => "saveas <path>";

    public async Task<int> ExecuteAsync(string[] args, ITerminalOutput output, ITerminalContext context, CancellationToken ct)
    {
        if (args.Length == 0) { output.WriteError("Usage: " + Usage); return 1; }
        var path = Path.IsPathRooted(args[0]) ? args[0] : Path.Combine(context.WorkingDirectory, args[0]);
        await context.IDE.SolutionExplorer.SaveFileAsync(path, ct).ConfigureAwait(false);
        output.WriteLine($"Saved as: {path}");
        return 0;
    }
}
