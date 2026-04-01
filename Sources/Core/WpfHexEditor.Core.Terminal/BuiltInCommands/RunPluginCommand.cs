//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Core.Terminal.BuiltInCommands;

public sealed class RunPluginCommand : ITerminalCommandProvider
{
    public string CommandName => "runplugin";
    public string Description => "Execute a terminal command contributed by a plugin.";
    public string Usage       => "runplugin <pluginId> [args…]";

    public Task<int> ExecuteAsync(string[] args, ITerminalOutput output, ITerminalContext context, CancellationToken ct)
    {
        if (args.Length == 0) { output.WriteError("Usage: " + Usage); return Task.FromResult(1); }
        // Plugin-registered commands are resolved directly via the TerminalCommandRegistry;
        // this command simply documents the pattern. Use the plugin's command name directly.
        output.WriteWarning("Use the plugin's command name directly (registered via ITerminalCommandProvider).");
        output.WriteLine($"Plugin ID hint: {args[0]}");
        return Task.FromResult(0);
    }
}
