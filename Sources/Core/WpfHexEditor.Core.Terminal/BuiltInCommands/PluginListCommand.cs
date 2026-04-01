//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using WpfHexEditor.SDK.Contracts;

namespace WpfHexEditor.Core.Terminal.BuiltInCommands;

public sealed class PluginListCommand : ITerminalCommandProvider
{
    public string CommandName => "plugin-list";
    public string Description => "List all installed plugins and their state.";
    public string Usage       => "plugin-list";

    public Task<int> ExecuteAsync(string[] args, ITerminalOutput output, ITerminalContext context, CancellationToken ct)
    {
        // Plugins are enumerated via the event bus or future IPluginQueryService;
        // for now output a placeholder — actual wiring done in WpfHexEditor.Terminal.
        output.WriteWarning("plugin-list: plugin query service not yet wired. Use Plugin Manager UI.");
        return Task.FromResult(0);
    }
}
