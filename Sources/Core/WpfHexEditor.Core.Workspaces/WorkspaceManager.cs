// ==========================================================
// Project: WpfHexEditor.Core.Workspaces
// File: WorkspaceManager.cs
// Description:
//     Default implementation of IWorkspaceManager.
//     Delegates all I/O to WorkspaceSerializer; fires lifecycle events.
// Architecture:
//     Stateful singleton — one active workspace at a time.
//     Thread-safe: all state mutations happen on the caller thread (UI thread).
// ==========================================================

namespace WpfHexEditor.Core.Workspaces;

/// <inheritdoc cref="IWorkspaceManager"/>
public sealed class WorkspaceManager : IWorkspaceManager
{
    private string? _currentName;
    private string? _currentPath;

    // ── IWorkspaceManager ─────────────────────────────────────────────────────

    public string? CurrentName => _currentName;
    public string? CurrentPath => _currentPath;
    public bool    IsOpen      => _currentPath is not null;

    public event EventHandler<WorkspaceOpenedEventArgs>? WorkspaceOpened;
    public event EventHandler?                            WorkspaceClosed;

    // ── Operations ────────────────────────────────────────────────────────────

    public async Task<WorkspaceState> NewAsync(
        string            name,
        string            filePath,
        WorkspaceCapture  capture,
        CancellationToken ct = default)
    {
        var state = BuildState(name, capture);
        await WorkspaceSerializer.WriteAsync(filePath, state, ct).ConfigureAwait(false);

        _currentName = name;
        _currentPath = filePath;
        WorkspaceOpened?.Invoke(this, new WorkspaceOpenedEventArgs(name, filePath));
        return state;
    }

    public async Task<WorkspaceState> OpenAsync(
        string            filePath,
        CancellationToken ct = default)
    {
        var state = await WorkspaceSerializer.ReadAsync(filePath, ct).ConfigureAwait(false);

        _currentName = state.Manifest.Name;
        _currentPath = filePath;
        WorkspaceOpened?.Invoke(this, new WorkspaceOpenedEventArgs(_currentName, filePath));
        return state;
    }

    public async Task SaveAsync(
        WorkspaceCapture  capture,
        CancellationToken ct = default)
    {
        if (_currentPath is null)
            throw new InvalidOperationException("No workspace is currently open.");

        var state = BuildState(_currentName ?? "Unnamed", capture);
        await WorkspaceSerializer.WriteAsync(_currentPath, state, ct).ConfigureAwait(false);
    }

    public async Task SaveAsAsync(
        string            filePath,
        WorkspaceCapture  capture,
        CancellationToken ct = default)
    {
        var state = BuildState(_currentName ?? "Unnamed", capture);
        await WorkspaceSerializer.WriteAsync(filePath, state, ct).ConfigureAwait(false);
        _currentPath = filePath;
    }

    public Task CloseAsync()
    {
        _currentName = null;
        _currentPath = null;
        WorkspaceClosed?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static WorkspaceState BuildState(string name, WorkspaceCapture capture) =>
        new()
        {
            Manifest = new WorkspaceManifest(
                Name:      name,
                CreatedAt: DateTime.UtcNow.ToString("o")),

            Layout   = capture.LayoutJson,

            Solution = new WorkspaceSolutionState(capture.SolutionPath),

            Files    = capture.OpenFilePaths
                              .Where(static p => !string.IsNullOrEmpty(p))
                              .Select(static p => new OpenFileEntry(p))
                              .ToList(),

            Settings = new WorkspaceSettingsOverride(capture.ThemeName),
        };
}
