// ==========================================================
// Project: WpfHexEditor.Core.Terminal
// File: BuiltInCommands/PluginReloadCommand.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-30
// Description:
//     Terminal command — reload a plugin in-place via CollectibleALC hot-reload.
//     Publishes PluginReloadRequestedEvent on the IDE event bus.
//     WpfPluginHost subscribes and calls ReloadPluginAsync (V2 hot-reload first,
//     then fallback to full unload/load cycle).
// ==========================================================

using WpfHexEditor.SDK.Events;

namespace WpfHexEditor.Core.Terminal.BuiltInCommands;

/// <summary>
/// Terminal command: <c>plugin reload &lt;plugin-id&gt;</c>
/// Triggers a hot-reload of the specified plugin via CollectibleALC cycle.
/// </summary>
public sealed class PluginReloadCommand : ITerminalCommandProvider
{
    public string CommandName => "plugin-reload";
    public string Description => "Reload a plugin in-place (CollectibleALC hot-reload).";
    public string Usage       => "plugin-reload <plugin-id>";

    public Task<int> ExecuteAsync(string[] args, ITerminalOutput output,
        ITerminalContext context, CancellationToken ct)
    {
        if (args.Length == 0)
        {
            output.WriteError("Usage: plugin-reload <plugin-id>");
            output.WriteInfo("Example: plugin-reload WpfHexEditor.Plugins.LSPTools");
            return Task.FromResult(1);
        }

        var pluginId = args[0].Trim();
        if (string.IsNullOrEmpty(pluginId))
        {
            output.WriteError("plugin-reload: plugin-id cannot be empty.");
            return Task.FromResult(1);
        }

        output.WriteInfo($"[PluginHost] Requesting reload of '{pluginId}'…");

        context.IDE.EventBus.Publish(new PluginReloadRequestedEvent
        {
            PluginId = pluginId,
        });

        return Task.FromResult(0);
    }
}
