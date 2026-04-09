// ==========================================================
// Project: WpfHexEditor.SDK
// File: Contracts/Services/IWorkspacePersistable.cs
// Description:
//     Optional interface for plugins that need to persist and restore
//     their own state as part of a .whidews workspace save/restore cycle.
//     Plugins opt in by calling IPluginCapabilityRegistry.RegisterWorkspacePersistable()
//     from within InitializeAsync.
// Architecture:
//     SDK layer only — no Core.Workspaces dependency from plugins.
// ==========================================================

namespace WpfHexEditor.SDK.Contracts.Services;

/// <summary>
/// Implemented by plugins that want to save and restore their own state
/// inside a <c>.whidews</c> workspace archive.
/// </summary>
/// <remarks>
/// The IDE invokes <see cref="CaptureWorkspaceState"/> on every workspace save
/// and <see cref="RestoreWorkspaceStateAsync"/> on every workspace open, after
/// layout, solution, and file tabs have been restored.
/// The returned value of <see cref="CaptureWorkspaceState"/> is serialized to JSON
/// and stored under the plugin's ID in <c>plugins.json</c> inside the archive.
/// </remarks>
public interface IWorkspacePersistable
{
    /// <summary>
    /// Returns this plugin's state as a JSON-serializable object.
    /// Use an anonymous type, a <c>Dictionary&lt;string, string&gt;</c>, or a named DTO.
    /// Return <see langword="null"/> to skip saving state for the current workspace.
    /// </summary>
    object? CaptureWorkspaceState();

    /// <summary>
    /// Restores state previously returned by <see cref="CaptureWorkspaceState"/>.
    /// </summary>
    /// <param name="json">
    /// Raw JSON string as stored in <c>plugins.json</c>.
    /// The plugin is responsible for deserializing its own DTO.
    /// </param>
    /// <param name="ct">Cancellation token for the restore operation.</param>
    Task RestoreWorkspaceStateAsync(string json, CancellationToken ct = default);
}
