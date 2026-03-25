// ==========================================================
// Project: WpfHexEditor.SDK
// File: Contracts/Services/IWorkspaceService.cs
// Description:
//     Plugin-facing view of the workspace system.
//     Exposes read-only workspace state and lifecycle events.
//     The App layer provides the implementation (WorkspaceServiceImpl).
// Architecture:
//     SDK layer only — no Core.Workspaces dependency from plugins.
// ==========================================================

namespace WpfHexEditor.SDK.Contracts.Services;

/// <summary>
/// Event arguments for the WorkspaceOpened event.
/// </summary>
public sealed class WorkspaceOpenedServiceEventArgs(string name, string path) : EventArgs
{
    /// <summary>Display name of the workspace.</summary>
    public string Name { get; } = name;

    /// <summary>File path of the .whidews file.</summary>
    public string Path { get; } = path;
}

/// <summary>
/// Plugin-facing service for querying the current workspace state.
/// Exposed via <see cref="IIDEHostContext.Workspace"/>.
/// </summary>
public interface IWorkspaceService
{
    /// <summary>Display name of the active workspace, or null when none is open.</summary>
    string? CurrentWorkspaceName { get; }

    /// <summary>File path of the active .whidews workspace, or null when none is open.</summary>
    string? CurrentWorkspacePath { get; }

    /// <summary>True when a workspace file is currently loaded.</summary>
    bool IsWorkspaceOpen { get; }

    /// <summary>Raised on the UI thread when a workspace is opened or created.</summary>
    event EventHandler<WorkspaceOpenedServiceEventArgs>? WorkspaceOpened;

    /// <summary>Raised on the UI thread when the active workspace is closed.</summary>
    event EventHandler? WorkspaceClosed;
}
