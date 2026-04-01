//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using WpfHexEditor.SDK.Models;

namespace WpfHexEditor.SDK.Contracts;

/// <summary>
/// Manages runtime permissions for loaded plugins.
/// </summary>
public interface IPermissionService
{
    /// <summary>
    /// Returns true if the specified permission is currently granted for the plugin.
    /// </summary>
    /// <param name="pluginId">Plugin identifier.</param>
    /// <param name="permission">Permission to check.</param>
    bool IsGranted(string pluginId, PluginPermission permission);

    /// <summary>
    /// Gets all currently granted permissions for a plugin as a combined flags value.
    /// </summary>
    /// <param name="pluginId">Plugin identifier.</param>
    PluginPermission GetGranted(string pluginId);

    /// <summary>
    /// Grants a permission to the specified plugin at runtime.
    /// Raises <see cref="PermissionChanged"/> if the state changes.
    /// </summary>
    void Grant(string pluginId, PluginPermission permission);

    /// <summary>
    /// Revokes a permission from the specified plugin at runtime.
    /// Raises <see cref="PermissionChanged"/>. Plugin must adapt gracefully.
    /// </summary>
    void Revoke(string pluginId, PluginPermission permission);

    /// <summary>
    /// Raised when a permission is granted or revoked for any plugin.
    /// Raised on the UI thread.
    /// </summary>
    event EventHandler<PermissionChangedEventArgs> PermissionChanged;
}

/// <summary>
/// Event arguments for permission change notifications.
/// </summary>
public sealed class PermissionChangedEventArgs : EventArgs
{
    /// <summary>Plugin identifier whose permission changed.</summary>
    public string PluginId { get; init; } = string.Empty;

    /// <summary>The permission that changed.</summary>
    public PluginPermission Permission { get; init; }

    /// <summary>True if the permission was granted; false if revoked.</summary>
    public bool IsGranted { get; init; }
}
