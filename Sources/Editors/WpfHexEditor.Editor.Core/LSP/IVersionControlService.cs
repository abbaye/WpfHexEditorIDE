// ==========================================================
// Project: WpfHexEditor.Editor.Core
// File: LSP/IVersionControlService.cs
// Description:
//     Generic version control contract — no git knowledge in consumers.
//     Implemented by WpfHexEditor.Plugins.Git (GitVersionControlService).
//     Injected into IDEHostContext.VersionControl.
// Architecture Notes:
//     Pattern: IBreakpointSource — generic data injection, plugin owns the impl.
//     BlameGutterControl receives BlameEntry[] and knows nothing about git.
// ==========================================================

namespace WpfHexEditor.Editor.Core.LSP;

// ── Domain records ────────────────────────────────────────────────────────────

/// <summary>Single blame annotation for one source line.</summary>
public sealed record BlameEntry(
    int    Line,
    string CommitHash,
    string AuthorName,
    DateTime Date,
    string Message);

/// <summary>A file that has been changed in the working tree / index.</summary>
public sealed record GitChangeEntry(string FilePath, GitChangeKind Kind);

/// <summary>Classification of a working-tree change.</summary>
public enum GitChangeKind { Modified, Added, Deleted, Renamed, Untracked, Staged }

// ── Contract ──────────────────────────────────────────────────────────────────

/// <summary>
/// Generic version control facade exposed to the IDE and all plugins.
/// All members that can be null return null when no repository is active.
/// </summary>
public interface IVersionControlService
{
    // ── Repository state ──────────────────────────────────────────────────────

    /// <summary>True when the currently active file belongs to a VCS repository.</summary>
    bool   IsRepo     { get; }

    /// <summary>Current branch name, or <c>null</c> when no repo is active.</summary>
    string? BranchName { get; }

    /// <summary>True when the working tree contains uncommitted changes.</summary>
    bool   IsDirty   { get; }

    /// <summary>
    /// Raised when repository state changes (new branch, dirty flag toggles, etc.).
    /// Always fired on the UI thread.
    /// </summary>
    event EventHandler? StatusChanged;

    // ── Async operations ──────────────────────────────────────────────────────

    /// <summary>Re-reads repository status from disk.</summary>
    Task RefreshAsync(CancellationToken ct = default);

    /// <summary>Returns per-line blame annotations for <paramref name="filePath"/>.</summary>
    Task<IReadOnlyList<BlameEntry>> GetBlameAsync(string filePath, CancellationToken ct = default);

    /// <summary>Returns all changed files in the working tree / index.</summary>
    Task<IReadOnlyList<GitChangeEntry>> GetChangedFilesAsync(CancellationToken ct = default);

    /// <summary>Returns the unified diff for <paramref name="filePath"/> against HEAD.</summary>
    Task<string> GetDiffAsync(string filePath, CancellationToken ct = default);

    // ── Staging operations ────────────────────────────────────────────────────

    Task StageAsync  (string filePath, CancellationToken ct = default);
    Task UnstageAsync(string filePath, CancellationToken ct = default);
    Task DiscardAsync(string filePath, CancellationToken ct = default);
}
