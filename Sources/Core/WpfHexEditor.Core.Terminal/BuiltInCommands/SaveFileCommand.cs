//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Core.Terminal.BuiltInCommands;

public sealed class SaveFileCommand : ITerminalCommandProvider
{
    public string CommandName => "savefile";
    public string Description => "Save the active file (or a named open file).";
    public string Usage       => "savefile [name]";

    public async Task<int> ExecuteAsync(string[] args, ITerminalOutput output, ITerminalContext context, CancellationToken ct)
    {
        var name = args.Length > 0 ? args[0] : null;
        await context.IDE.SolutionExplorer.SaveFileAsync(name, ct).ConfigureAwait(false);
        output.WriteLine(name is null ? "Active file saved." : $"Saved: {name}");
        return 0;
    }
}
