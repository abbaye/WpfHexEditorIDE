// ==========================================================
// Project: WpfHexEditor.SDK
// File: Events/PluginReloadEvents.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-30
// Description:
//     Plugin reload event published by the terminal "plugin reload <id>" command.
//     WpfPluginHost subscribes to this via IDEEvents and calls ReloadPluginAsync.
// ==========================================================

namespace WpfHexEditor.SDK.Events;

/// <summary>
/// Published by <c>plugin reload &lt;id&gt;</c> terminal command.
/// WpfPluginHost subscribes and calls <c>ReloadPluginAsync(PluginId)</c>.
/// </summary>
public sealed class PluginReloadRequestedEvent
{
    /// <summary>ID of the plugin to reload (e.g. "WpfHexEditor.Plugins.LSPTools").</summary>
    public string PluginId { get; init; } = string.Empty;
}
