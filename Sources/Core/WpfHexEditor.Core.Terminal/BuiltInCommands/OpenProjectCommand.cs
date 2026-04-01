//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Core.Terminal.BuiltInCommands;

public sealed class OpenProjectCommand : ITerminalCommandProvider
{
    public string CommandName => "openproject";
    public string Description => "Activate a project node in Solution Explorer by name.";
    public string Usage       => "openproject <name>";

    public async Task<int> ExecuteAsync(string[] args, ITerminalOutput output, ITerminalContext context, CancellationToken ct)
    {
        if (args.Length == 0) { output.WriteError("Usage: " + Usage); return 1; }
        await context.IDE.SolutionExplorer.OpenProjectAsync(string.Join(" ", args), ct).ConfigureAwait(false);
        output.WriteLine($"Opened project: {string.Join(" ", args)}");
        return 0;
    }
}
