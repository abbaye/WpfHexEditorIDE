//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

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
    Task ReloadAsync(CancellationToken ct = default);

    /// <summary>Gets whether the plugin currently supports hot-reload.</summary>
    bool SupportsHotReload { get; }
}
