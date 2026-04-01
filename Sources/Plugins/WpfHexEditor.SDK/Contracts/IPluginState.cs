//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

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
