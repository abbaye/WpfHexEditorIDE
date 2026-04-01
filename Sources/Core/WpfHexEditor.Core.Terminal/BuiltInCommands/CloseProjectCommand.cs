//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Core.Terminal.BuiltInCommands;

public sealed class CloseProjectCommand : ITerminalCommandProvider
{
    public string CommandName => "closeproject";
    public string Description => "Close a project by name.";
    public string Usage       => "closeproject <name>";

    public async Task<int> ExecuteAsync(string[] args, ITerminalOutput output, ITerminalContext context, CancellationToken ct)
    {
        if (args.Length == 0) { output.WriteError("Usage: " + Usage); return 1; }
        await context.IDE.SolutionExplorer.CloseProjectAsync(string.Join(" ", args), ct).ConfigureAwait(false);
        output.WriteLine($"Closed project: {string.Join(" ", args)}");
        return 0;
    }
}
