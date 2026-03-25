// Project      : WpfHexEditorControl
// File         : Services/CompareFileLaunchService.cs
// Description  : Orchestrates the Compare Files flow: shows the VS Code-style file picker
//                (CompareFilePickerWindow) for any missing path, calls the supplied docking
//                callback to open the DiffViewer tab, and persists history to ComparisonSettings.
// Architecture : Stateless service; UI operations run on the calling (UI) thread.
//                All async coordination is via async/await with CancellationToken.
//                Temp-file cleanup is tracked per-session via _tempFiles HashSet.

using System.IO;
using System.Windows;
using WpfHexEditor.App.Dialogs;
using WpfHexEditor.Editor.Core.Documents;
using WpfHexEditor.Core.Options;

namespace WpfHexEditor.App.Services;

/// <summary>
/// Coordinates the full Compare Files workflow from any entry point:
/// toolbar, Command Palette, Solution Explorer, HexEditor context menu, tab bar, etc.
/// </summary>
public sealed class CompareFileLaunchService
{
    // ── Dependencies (injected via ctor) ──────────────────────────────────────

    private readonly Window            _ownerWindow;
    private readonly IDocumentManager? _documentManager;
    private readonly ComparisonSettings _settings;
    private readonly AppSettingsService _settingsService;

    /// <summary>
    /// Callback that creates and docks the DiffViewer for two file paths.
    /// The implementation lives in MainWindow and has access to the docking engine.
    /// </summary>
    private readonly Action<string, string> _openDiffViewer;

    // ── Temp file tracking ────────────────────────────────────────────────────
    // Maps temp paths to the git ref they were extracted from (for labelling).
    private readonly HashSet<string> _tempFiles = [];

    // ── Constructor ───────────────────────────────────────────────────────────

    public CompareFileLaunchService(
        Window              ownerWindow,
        IDocumentManager?   documentManager,
        ComparisonSettings  settings,
        AppSettingsService  settingsService,
        Action<string, string> openDiffViewer)
    {
        _ownerWindow     = ownerWindow;
        _documentManager = documentManager;
        _settings        = settings;
        _settingsService = settingsService;
        _openDiffViewer  = openDiffViewer;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Launches the compare flow.
    /// <list type="bullet">
    ///   <item>Both paths provided → open directly.</item>
    ///   <item>Only <paramref name="leftPath"/> provided → pick right file.</item>
    ///   <item>Neither provided → pick left then right.</item>
    /// </list>
    /// </summary>
    public async Task LaunchAsync(
        string? leftPath  = null,
        string? rightPath = null,
        CancellationToken ct = default)
    {
        // --- Pick LEFT file if missing ----------------------------------------
        if (string.IsNullOrEmpty(leftPath))
        {
            var recentLeftPaths = BuildRecentPaths(side: "left");
            leftPath = await CompareFilePickerWindow.ShowAsync(
                owner           : _ownerWindow,
                promptTitle     : "Select LEFT file to compare",
                documentManager : _documentManager,
                recentFiles     : recentLeftPaths,
                activeEditorPath: GetActiveEditorPath());

            if (leftPath is null) return;   // user cancelled
        }

        ct.ThrowIfCancellationRequested();

        // --- Pick RIGHT file if missing ----------------------------------------
        if (string.IsNullOrEmpty(rightPath))
        {
            var recentRightPaths = BuildRecentPaths(side: "right", exclude: leftPath);
            rightPath = await CompareFilePickerWindow.ShowAsync(
                owner           : _ownerWindow,
                promptTitle     : $"Select RIGHT file — comparing with {Path.GetFileName(leftPath)}",
                documentManager : _documentManager,
                recentFiles     : recentRightPaths,
                activeEditorPath: GetActiveEditorPathExcluding(leftPath));

            if (rightPath is null) return;   // user cancelled
        }

        ct.ThrowIfCancellationRequested();

        // --- Open viewer --------------------------------------------------------
        _openDiffViewer(leftPath, rightPath);

        // --- Persist history ----------------------------------------------------
        _settings.AddToHistory(leftPath, rightPath);
        _settingsService.Save();
    }

    /// <summary>
    /// Convenience overload: launch with only the left path resolved (e.g. from Solution Explorer).
    /// </summary>
    public Task LaunchWithLeftAsync(string leftPath, CancellationToken ct = default)
        => LaunchAsync(leftPath, null, ct);

    /// <summary>
    /// Re-opens the most recent comparison from history, if any.
    /// </summary>
    public Task ReopenLastAsync(CancellationToken ct = default)
    {
        var last = _settings.RecentComparisons.FirstOrDefault();
        if (last is null) return Task.CompletedTask;
        return LaunchAsync(last.LeftPath, last.RightPath, ct);
    }

    /// <summary>
    /// Returns true if the history list has at least one entry (used to enable
    /// the "Reopen Last" command).
    /// </summary>
    public bool HasHistory => _settings.RecentComparisons.Count > 0;

    // ── Temp file management ──────────────────────────────────────────────────

    /// <summary>Registers a temp file created by git extraction for later cleanup.</summary>
    public void TrackTempFile(string path) => _tempFiles.Add(path);

    /// <summary>Deletes all tracked temp files (call on DiffViewer.Closed).</summary>
    public void CleanupTempFiles()
    {
        foreach (var path in _tempFiles)
        {
            try { File.Delete(path); } catch { /* best-effort */ }
        }
        _tempFiles.Clear();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds the "recent files" list for the picker from comparison history.
    /// <paramref name="side"/> is "left" or "right".
    /// </summary>
    private IReadOnlyList<string> BuildRecentPaths(string side, string? exclude = null)
    {
        return _settings.RecentComparisons
            .Select(e => side == "left" ? e.LeftPath : e.RightPath)
            .Where(p => !string.IsNullOrEmpty(p) &&
                        !string.Equals(p, exclude, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToList();
    }

    /// <summary>Returns the file path of the currently active editor, or <c>null</c>.</summary>
    private string? GetActiveEditorPath()
    {
        if (_documentManager is null) return null;
        return _documentManager.ActiveDocument?.FilePath;
    }

    /// <summary>
    /// Returns the file path of the active editor, but only if it differs from
    /// <paramref name="exclude"/> (avoids offering the same file for both sides).
    /// </summary>
    private string? GetActiveEditorPathExcluding(string? exclude)
    {
        var active = GetActiveEditorPath();
        return string.Equals(active, exclude, StringComparison.OrdinalIgnoreCase)
            ? null
            : active;
    }
}
