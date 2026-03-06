// ==========================================================
// Project: WpfHexEditor.SDK
// File: IPluginState.cs
// Author: Auto
// Created: 2026-03-06
// Description:
//     Optional interface a plugin can implement to support state serialization
//     and restoration across IDE sessions.
//
// Architecture Notes:
//     PluginStateSerializer in PluginHost calls Serialize() on shutdown and
//     Deserialize() on the next load, passing the previously saved JSON string.
//     State is stored at %AppData%/WpfHexEditor/Plugins/{PluginId}.json.
//
// ==========================================================

namespace WpfHexEditor.SDK.Contracts;

/// <summary>
/// Optional contract for plugins that support state persistence across sessions.
/// </summary>
public interface IPluginState
{
    /// <summary>
    /// Serializes the current plugin state to a JSON string.
    /// Called by PluginHost on IDE shutdown or plugin unload.
    /// </summary>
    /// <returns>A JSON string representing the plugin's current state, or null if stateless.</returns>
    string? Serialize();

    /// <summary>
    /// Restores the plugin state from a previously serialized JSON string.
    /// Called by PluginHost after <c>InitializeAsync</c> completes.
    /// </summary>
    /// <param name="state">Previously serialized JSON state string.</param>
    void Deserialize(string state);
}
