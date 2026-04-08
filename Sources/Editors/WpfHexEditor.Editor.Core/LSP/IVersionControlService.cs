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
    int      Line,
    string   CommitHash,
    string   AuthorName,
    DateTime Date,
    string   Message);

/// <summary>A file that has been changed in the working tree / index.</summary>
public sealed record GitChangeEntry(string FilePath, GitChangeKind Kind);

/// <summary>Classification of a working-tree change.</summary>
public enum GitChangeKind { Modified, Added, Deleted, Renamed, Untracked, Staged, Conflicted }

/// <summary>Branch information returned by <see cref="IVersionControlService.GetBranchesAsync"/>.</summary>
public sealed record BranchInfo(
    string  Name,
    bool    IsCurrent,
    bool    IsRemote,
    string? UpstreamName);

/// <summary>A stash entry.</summary>
public sealed record StashEntry(
    int      Index,
    string   Message,
    DateTime Date);

/// <summary>Commit metadata returned by <see cref="IVersionControlService.GetLogAsync"/>.</summary>
public sealed record CommitInfo(
    string   Hash,
    string   ShortHash,
    string   Message,
    string   AuthorName,
    DateTime Date,
    string[] ChangedFiles);

/// <summary>Ahead/behind counts relative to upstream tracking branch.</summary>
public sealed record AheadBehind(int Ahead, int Behind);

/// <summary>A single unified-diff hunk.</summary>
public sealed record DiffHunk(
    int      OldStart,
    int      OldCount,
    int      NewStart,
    int      NewCount,
    string   Header,
    string[] Lines);

// ── Contract ──────────────────────────────────────────────────────────────────

/// <summary>
/// Generic version control facade exposed to the IDE and all plugins.
/// All members that can be null return null when no repository is active.
/// </summary>
public interface IVersionControlService
{
    // ── Repository state ──────────────────────────────────────────────────────

    /// <summary>True when the currently active file belongs to a VCS repository.</summary>
    bool    IsRepo     { get; }

    /// <summary>Current branch name, or <c>null</c> when no repo is active.</summary>
    string? BranchName { get; }

    /// <summary>True when the working tree contains uncommitted changes.</summary>
    bool    IsDirty    { get; }

    /// <summary>
    /// Raised when repository state changes (new branch, dirty flag toggles, etc.).
    /// Always fired on the UI thread.
    /// </summary>
    event EventHandler? StatusChanged;

    // ── Async read operations ─────────────────────────────────────────────────

    /// <summary>Re-reads repository status from disk.</summary>
    Task RefreshAsync(CancellationToken ct = default);

    /// <summary>Returns per-line blame annotations for <paramref name="filePath"/>.</summary>
    Task<IReadOnlyList<BlameEntry>> GetBlameAsync(string filePath, CancellationToken ct = default);

    /// <summary>Returns all changed files in the working tree / index.</summary>
    Task<IReadOnlyList<GitChangeEntry>> GetChangedFilesAsync(CancellationToken ct = default);

    /// <summary>Returns the unified diff for <paramref name="filePath"/> against HEAD.</summary>
    Task<string> GetDiffAsync(string filePath, CancellationToken ct = default);

    /// <summary>Returns parsed diff hunks for <paramref name="filePath"/> against HEAD.</summary>
    Task<IReadOnlyList<DiffHunk>> GetDiffHunksAsync(string filePath, CancellationToken ct = default);

    // ── Staging operations ────────────────────────────────────────────────────

    Task StageAsync  (string filePath, CancellationToken ct = default);
    Task UnstageAsync(string filePath, CancellationToken ct = default);
    Task DiscardAsync(string filePath, CancellationToken ct = default);

    // ── Commit ────────────────────────────────────────────────────────────────

    /// <summary>Creates a commit with <paramref name="message"/>. Pass <paramref name="amend"/> to amend the last commit.</summary>
    Task CommitAsync(string message, bool amend = false, CancellationToken ct = default);

    // ── Remote operations ─────────────────────────────────────────────────────

    Task PushAsync (bool force = false, CancellationToken ct = default);
    Task PullAsync (CancellationToken ct = default);
    Task FetchAsync(CancellationToken ct = default);

    /// <summary>Returns ahead/behind counts relative to the upstream tracking branch.</summary>
    Task<AheadBehind> GetAheadBehindAsync(CancellationToken ct = default);

    // ── Branch operations ─────────────────────────────────────────────────────

    Task<IReadOnlyList<BranchInfo>> GetBranchesAsync(CancellationToken ct = default);
    Task SwitchBranchAsync(string name, CancellationToken ct = default);
    Task CreateBranchAsync(string name, bool checkout = true, CancellationToken ct = default);
    Task DeleteBranchAsync(string name, bool force = false, CancellationToken ct = default);

    // ── Stash operations ──────────────────────────────────────────────────────

    Task<IReadOnlyList<StashEntry>> GetStashListAsync(CancellationToken ct = default);
    Task StashAsync   (string? message = null, bool includeUntracked = true, CancellationToken ct = default);
    Task StashPopAsync (int index = 0, CancellationToken ct = default);
    Task StashDropAsync(int index,     CancellationToken ct = default);

    // ── History ───────────────────────────────────────────────────────────────

    /// <summary>Returns the commit log, optionally scoped to <paramref name="filePath"/>.</summary>
    Task<IReadOnlyList<CommitInfo>> GetLogAsync(
        int maxCount = 100, string? filePath = null, CancellationToken ct = default);

    /// <summary>Returns the full patch text for a single commit (git show --stat -p).</summary>
    Task<string> ShowCommitAsync(string hash, CancellationToken ct = default);
}
