// ==========================================================
// Project: WpfHexEditor.Core.Workspaces
// File: IWorkspaceManager.cs
// Description:
//     Contract for creating, opening, saving and closing .whidews workspaces.
//     The App layer implements this interface and injects the necessary
//     capture/restore delegates at construction time.
// Architecture:
//     Core layer only. No UI, no WPF, no App dependencies.
// ==========================================================

namespace WpfHexEditor.Core.Workspaces;

/// <summary>
/// Manages the lifecycle of <c>.whidews</c> workspace files.
/// </summary>
public interface IWorkspaceManager
{
    /// <summary>Display name of the currently open workspace, or null.</summary>
    string? CurrentName { get; }

    /// <summary>File path of the currently open workspace, or null.</summary>
    string? CurrentPath { get; }

    /// <summary>True when a workspace is open.</summary>
    bool IsOpen { get; }

    /// <summary>Fired after a workspace is successfully opened or created.</summary>
    event EventHandler<WorkspaceOpenedEventArgs>? WorkspaceOpened;

    /// <summary>Fired after the current workspace is closed.</summary>
    event EventHandler? WorkspaceClosed;

    /// <summary>
    /// Creates a new workspace, snapshots the current IDE state, saves it to
    /// <paramref name="filePath"/>, and makes it the active workspace.
    /// </summary>
    Task<WorkspaceState> NewAsync(
        string            name,
        string            filePath,
        WorkspaceCapture  capture,
        CancellationToken ct = default);

    /// <summary>
    /// Opens an existing .whidews file and returns its state for the App layer
    /// to apply (layout, solution, open files, theme).
    /// </summary>
    Task<WorkspaceState> OpenAsync(
        string            filePath,
        CancellationToken ct = default);

    /// <summary>
    /// Saves a snapshot to the currently open workspace file.
    /// Throws <see cref="InvalidOperationException"/> when no workspace is open.
    /// </summary>
    Task SaveAsync(
        WorkspaceCapture  capture,
        CancellationToken ct = default);

    /// <summary>
    /// Saves a snapshot to <paramref name="filePath"/> and makes it the active path.
    /// </summary>
    Task SaveAsAsync(
        string            filePath,
        WorkspaceCapture  capture,
        CancellationToken ct = default);

    /// <summary>
    /// Clears the current workspace without touching open documents or layout.
    /// </summary>
    Task CloseAsync();
}
