// ==========================================================
// Project: WpfHexEditor.SDK
// File: IWpfHexEditorPluginV2.cs
// Author: Auto
// Created: 2026-03-06
// Description:
//     Extended plugin interface for V2 SDK — adds hot-reload support.
//     Plugins implementing this interface can reload without full unload/reload cycle.
//
// Architecture Notes:
//     Backward compatible — V1 plugins still work with PluginHost.
//     ReloadAsync is called when the user triggers "Reload" in Plugin Manager
//     for plugins that support it; V1 plugins fall back to full unload/reload.
//
// ==========================================================

namespace WpfHexEditor.SDK.Contracts;

/// <summary>
/// Extended plugin interface (V2) adding hot-reload support.
/// Plugins implementing this can be reloaded in-place without full unload/load cycle.
/// </summary>
public interface IWpfHexEditorPluginV2 : IWpfHexEditorPlugin
{
    /// <summary>
    /// Reloads the plugin in-place, refreshing its state without full unload.
    /// Useful for configuration reloads or UI refresh.
    /// Implements graceful degradation: if ReloadAsync fails, PluginHost falls back
    /// to a full <c>ShutdownAsync</c> + <c>InitializeAsync</c> cycle.
    /// </summary>
    Task ReloadAsync();

    /// <summary>Gets whether the plugin currently supports hot-reload.</summary>
    bool SupportsHotReload { get; }
}
