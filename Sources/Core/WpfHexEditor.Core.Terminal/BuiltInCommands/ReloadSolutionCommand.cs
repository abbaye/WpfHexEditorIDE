//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Core.Terminal.BuiltInCommands;

public sealed class ReloadSolutionCommand : ITerminalCommandProvider
{
    public string CommandName => "reloadsolution";
    public string Description => "Reload the active solution from disk.";
    public string Usage       => "reloadsolution";

    public async Task<int> ExecuteAsync(string[] args, ITerminalOutput output, ITerminalContext context, CancellationToken ct)
    {
        await context.IDE.SolutionExplorer.ReloadSolutionAsync(ct).ConfigureAwait(false);
        output.WriteLine("Solution reloaded.");
        return 0;
    }
}
