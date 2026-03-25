// ==========================================================
// Project: WpfHexEditor.App
// File: Services/WorkspaceServiceImpl.cs
// Description:
//     Bridges IWorkspaceService (SDK) to WorkspaceManager (Core.Workspaces).
//     Adapts WorkspaceOpenedEventArgs → WorkspaceOpenedServiceEventArgs
//     and marshals events to the UI thread.
// Architecture:
//     App layer only — not referenced by plugins directly.
// ==========================================================

using System.Windows;
using WpfHexEditor.Core.Workspaces;
using WpfHexEditor.SDK.Contracts.Services;

namespace WpfHexEditor.App.Services;

/// <summary>
/// App-level implementation of <see cref="IWorkspaceService"/>.
/// Wraps <see cref="IWorkspaceManager"/> for plugin consumption.
/// </summary>
public sealed class WorkspaceServiceImpl : IWorkspaceService
{
    private readonly IWorkspaceManager _manager;

    public WorkspaceServiceImpl(IWorkspaceManager manager)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));

        _manager.WorkspaceOpened += OnManagerOpened;
        _manager.WorkspaceClosed += OnManagerClosed;
    }

    // ── IWorkspaceService ─────────────────────────────────────────────────────

    public string? CurrentWorkspaceName => _manager.CurrentName;
    public string? CurrentWorkspacePath => _manager.CurrentPath;
    public bool    IsWorkspaceOpen      => _manager.IsOpen;

    public event EventHandler<WorkspaceOpenedServiceEventArgs>? WorkspaceOpened;
    public event EventHandler?                                   WorkspaceClosed;

    // ── Event forwarding ──────────────────────────────────────────────────────

    private void OnManagerOpened(object? sender, WorkspaceOpenedEventArgs e)
    {
        var args = new WorkspaceOpenedServiceEventArgs(e.Name, e.Path);
        Application.Current?.Dispatcher.InvokeAsync(
            () => WorkspaceOpened?.Invoke(this, args));
    }

    private void OnManagerClosed(object? sender, EventArgs e)
    {
        Application.Current?.Dispatcher.InvokeAsync(
            () => WorkspaceClosed?.Invoke(this, EventArgs.Empty));
    }
}
