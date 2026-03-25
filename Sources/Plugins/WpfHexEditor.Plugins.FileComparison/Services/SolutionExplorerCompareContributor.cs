// Project      : WpfHexEditorControl
// File         : Services/SolutionExplorerCompareContributor.cs
// Description  : Contributes "Compare with…" sub-items to the Solution Explorer context menu
//                for File nodes.  Git-aware items (Compare with HEAD / Branch / Commit) are
//                shown only when the file belongs to a git repository.
// Architecture : ISolutionExplorerContextMenuContributor — GetContextMenuItems() is synchronous
//                and O(1); git detection uses a cached repo-root lookup.
//                Command execution delegates to registered IDE commands via ICommandRegistry
//                so that the CompareFileLaunchService (MainWindow) handles all UI/docking.

using WpfHexEditor.Core.Diff.Services;
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.SDK.Contracts;

namespace WpfHexEditor.Plugins.FileComparison.Services;

/// <summary>
/// Injects "Compare with…" items into the Solution Explorer right-click context menu for files.
/// </summary>
internal sealed class SolutionExplorerCompareContributor : ISolutionExplorerContextMenuContributor
{
    private readonly IIDEHostContext _context;
    private readonly GitDiffService  _git = new();

    // Cache: nodePath → (repoRoot or null), so IsGitRepository() doesn't walk FS on every click
    private readonly Dictionary<string, string?> _gitRootCache =
        new(StringComparer.OrdinalIgnoreCase);

    public SolutionExplorerCompareContributor(IIDEHostContext context)
        => _context = context;

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
                command:   new RelayCommand(_ => InvokeCompareLeft(nodePath)),
                iconGlyph: "\uE8A5"),

            // Always-visible: nodePath as left, picker for right
            SolutionContextMenuItem.Item(
                header:    "Compare with Another File…",
                command:   new RelayCommand(_ => InvokeCommand(CmdCompareFiles, nodePath)),
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
                iconGlyph: "\uE8B5"));  // BranchMerge-like

            items.Add(SolutionContextMenuItem.Item(
                header:    "Compare with Branch…",
                command:   new RelayCommand(_ => _ = InvokeCompareWithRefAsync(nodePath, repoRoot,
                    GitRefPickerMode.Branches)),
                iconGlyph: "\uE71B"));  // Source

            items.Add(SolutionContextMenuItem.Item(
                header:    "Compare with Commit…",
                command:   new RelayCommand(_ => _ = InvokeCompareWithRefAsync(nodePath, repoRoot,
                    GitRefPickerMode.Commits)),
                iconGlyph: "\uE81C"));  // Tag
        }

        return items;
    }

    // ── Command ID constants (mirrors CommandIds in WpfHexEditor.Commands) ────
    // Duplicated as string literals to avoid a dependency on WpfHexEditor.Commands.
    private const string CmdCompareWithActiveEditor = "View.Compare.WithActiveEditor";
    private const string CmdCompareWithHead         = "View.Compare.WithHead";
    private const string CmdCompareFiles            = "View.CompareFiles";

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>Invokes an IDE command that was pre-registered by MainWindow.Commands.cs.</summary>
    private void InvokeCommand(string commandId, object? parameter = null)
    {
        var cmd = _context.CommandRegistry?.Find(commandId)?.Command;
        cmd?.Execute(parameter);
    }

    private void InvokeCompareLeft(string leftPath)
        => InvokeCommand(CmdCompareWithActiveEditor, leftPath);

    private async Task InvokeCompareWithHeadAsync(string filePath, string repoRoot)
    {
        var tempPath = await _git.ExtractRefVersionAsync(repoRoot, "HEAD", filePath);
        if (tempPath is null) return;
        InvokeCommand(CmdCompareFiles, new[] { tempPath, filePath });
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

        // Extract the first ref (oldest HEAD is a meaningful default);
        // branch/commit picker UI requires a WPF Window, so the App shell handles the picker
        // for the full "Compare with HEAD" command — this path is for direct git ref picks.
        var tempPath = await _git.ExtractRefVersionAsync(repoRoot, refs[0], filePath);
        if (tempPath is null) return;

        InvokeCommand(CmdCompareFiles, new[] { tempPath, filePath });
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
