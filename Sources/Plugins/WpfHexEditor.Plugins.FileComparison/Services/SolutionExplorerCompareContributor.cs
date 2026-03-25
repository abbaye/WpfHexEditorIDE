// Project      : WpfHexEditorControl
// File         : Services/SolutionExplorerCompareContributor.cs
// Description  : Contributes "Compare with…" sub-items to the Solution Explorer context menu
//                for File nodes.  Git-aware items (Compare with HEAD / Branch / Commit) are
//                shown only when the file belongs to a git repository.
// Architecture : ISolutionExplorerContextMenuContributor — GetContextMenuItems() is synchronous
//                and O(1); git detection uses a cached repo-root lookup.
//                Action delegates injected at construction time (same pattern as
//                ClassDiagramContextMenuContributor) — avoids IIDEHostContext.CommandRegistry
//                DIM null issue when called from a plugin ALC.

using WpfHexEditor.Core.Diff.Services;
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.SDK.Contracts;

namespace WpfHexEditor.Plugins.FileComparison.Services;

/// <summary>
/// Injects "Compare with…" items into the Solution Explorer right-click context menu for files.
/// Action callbacks are injected by <see cref="FileComparisonPlugin"/> so execution never goes
/// through the command registry (which may return null from a plugin ALC via the DIM default).
/// </summary>
internal sealed class SolutionExplorerCompareContributor : ISolutionExplorerContextMenuContributor
{
    private readonly IIDEHostContext        _context;
    private readonly GitDiffService         _git = new();
    private readonly Func<string, string?, Task> _compareWithFile;
    private readonly Action<string>              _compareWithActiveEditor;

    // Cache: nodePath → (repoRoot or null), so IsGitRepository() doesn't walk FS on every click
    private readonly Dictionary<string, string?> _gitRootCache =
        new(StringComparer.OrdinalIgnoreCase);

    /// <param name="context">Host context (used for git-root caching only).</param>
    /// <param name="compareWithFile">
    ///   Called with (leftPath, rightPath?). When rightPath is null the panel pre-fills File 1
    ///   only and the user picks File 2 in the panel.
    /// </param>
    /// <param name="compareWithActiveEditor">
    ///   Called with leftPath; the delegate resolves the active document as right side.
    /// </param>
    public SolutionExplorerCompareContributor(
        IIDEHostContext              context,
        Func<string, string?, Task>  compareWithFile,
        Action<string>               compareWithActiveEditor)
    {
        _context                 = context;
        _compareWithFile         = compareWithFile;
        _compareWithActiveEditor = compareWithActiveEditor;
    }

    // ── ISolutionExplorerContextMenuContributor ────────────────────────────────

    public IReadOnlyList<SolutionContextMenuItem> GetContextMenuItems(string nodeKind, string? nodePath)
    {
        if (nodeKind != "File" || string.IsNullOrEmpty(nodePath))
            return [];

        var items = new List<SolutionContextMenuItem>
        {
            // Always-visible: compare with the currently active editor
            SolutionContextMenuItem.Item(
                header:    "Compare with Active Editor",
                command:   new RelayCommand(_ => _compareWithActiveEditor(nodePath)),
                iconGlyph: "\uE8A5"),

            // Always-visible: nodePath as left, picker for right (user selects in panel)
            SolutionContextMenuItem.Item(
                header:    "Compare with Another File…",
                command:   new RelayCommand(_ => _ = _compareWithFile(nodePath, null)),
                iconGlyph: "\uE8B7"),
        };

        // Git-aware items — only add when a .git folder exists
        var repoRoot = GetCachedRepoRoot(nodePath);
        if (repoRoot is not null)
        {
            items.Add(SolutionContextMenuItem.Separator());

            items.Add(SolutionContextMenuItem.Item(
                header:    "Compare with HEAD (Git)",
                command:   new RelayCommand(_ => _ = InvokeCompareWithHeadAsync(nodePath, repoRoot)),
                iconGlyph: "\uE8B5"));

            items.Add(SolutionContextMenuItem.Item(
                header:    "Compare with Branch…",
                command:   new RelayCommand(_ => _ = InvokeCompareWithRefAsync(nodePath, repoRoot,
                    GitRefPickerMode.Branches)),
                iconGlyph: "\uE71B"));

            items.Add(SolutionContextMenuItem.Item(
                header:    "Compare with Commit…",
                command:   new RelayCommand(_ => _ = InvokeCompareWithRefAsync(nodePath, repoRoot,
                    GitRefPickerMode.Commits)),
                iconGlyph: "\uE81C"));
        }

        return items;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task InvokeCompareWithHeadAsync(string filePath, string repoRoot)
    {
        var tempPath = await _git.ExtractRefVersionAsync(repoRoot, "HEAD", filePath);
        if (tempPath is null) return;
        await _compareWithFile(tempPath, filePath);
    }

    private async Task InvokeCompareWithRefAsync(string filePath, string repoRoot, GitRefPickerMode mode)
    {
        IReadOnlyList<string> refs;
        if (mode == GitRefPickerMode.Branches)
            refs = await _git.GetBranchesAsync(repoRoot);
        else
        {
            var commits = await _git.GetRecentCommitsAsync(repoRoot, count: 30);
            refs = commits.Select(c => c.Hash).ToList();
        }

        if (refs.Count == 0) return;

        var tempPath = await _git.ExtractRefVersionAsync(repoRoot, refs[0], filePath);
        if (tempPath is null) return;

        await _compareWithFile(tempPath, filePath);
    }

    private string? GetCachedRepoRoot(string filePath)
    {
        if (_gitRootCache.TryGetValue(filePath, out var cached))
            return cached;

        var root = _git.GetRepoRoot(filePath);
        _gitRootCache[filePath] = root;
        return root;
    }

    private enum GitRefPickerMode { Branches, Commits }
}
