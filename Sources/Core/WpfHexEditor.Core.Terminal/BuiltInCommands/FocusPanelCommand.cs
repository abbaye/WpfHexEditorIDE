//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Core.Terminal.BuiltInCommands;

public sealed class FocusPanelCommand : ITerminalCommandProvider
{
    public string CommandName => "focuspanel";
    public string Description => "Give keyboard focus to a dockable panel by its UI id.";
    public string Usage       => "focuspanel <id>";

    public Task<int> ExecuteAsync(string[] args, ITerminalOutput output, ITerminalContext context, CancellationToken ct)
    {
        if (args.Length == 0) { output.WriteError("Usage: " + Usage); return Task.FromResult(1); }
        context.IDE.UIRegistry.FocusPanel(args[0]);
        output.WriteLine($"Focus set to panel: {args[0]}");
        return Task.FromResult(0);
    }
}
