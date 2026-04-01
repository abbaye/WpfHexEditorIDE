//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Core.Terminal.BuiltInCommands;

public sealed class CloseSolutionCommand : ITerminalCommandProvider
{
    public string CommandName => "closesolution";
    public string Description => "Close the active solution.";
    public string Usage       => "closesolution";

    public async Task<int> ExecuteAsync(string[] args, ITerminalOutput output, ITerminalContext context, CancellationToken ct)
    {
        await context.IDE.SolutionExplorer.CloseSolutionAsync(ct).ConfigureAwait(false);
        output.WriteLine("Solution closed.");
        return 0;
    }
}
