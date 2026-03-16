//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.SDK.Models;

/// <summary>
/// Represents the current lifecycle state of a plugin managed by PluginHost.
/// </summary>
public enum PluginState
{
    /// <summary>Plugin has not been loaded yet.</summary>
    Unloaded,

    /// <summary>
    /// Plugin has been discovered and validated but is not yet loaded.
    /// Awaiting an activation trigger (file extension match, command, or manual load).
    /// </summary>
    Dormant,

    /// <summary>Plugin is currently being loaded (manifest validated, assembly loading).</summary>
    Loading,

    /// <summary>Plugin is loaded and active.</summary>
    Loaded,

    /// <summary>Plugin has been explicitly disabled by the user.</summary>
    Disabled,

    /// <summary>Plugin threw an unhandled exception and has been deactivated.</summary>
    Faulted,

    /// <summary>
    /// Plugin manifest declares incompatible version constraints
    /// (IDE version, SDK version) and cannot be loaded.
    /// </summary>
    Incompatible
}
