//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Core.Terminal.BuiltInCommands;

public sealed class TogglePanelCommand : ITerminalCommandProvider
{
    public string CommandName => "togglepanel";
    public string Description => "Toggle visibility of a dockable panel by its UI id.";
    public string Usage       => "togglepanel <id>";

    public Task<int> ExecuteAsync(string[] args, ITerminalOutput output, ITerminalContext context, CancellationToken ct)
    {
        if (args.Length == 0) { output.WriteError("Usage: " + Usage); return Task.FromResult(1); }
        context.IDE.UIRegistry.TogglePanel(args[0]);
        output.WriteLine($"Panel toggled: {args[0]}");
        return Task.FromResult(0);
    }
}
