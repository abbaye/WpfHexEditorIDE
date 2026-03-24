// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Services/WorkspaceFileCache.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-17
// Description:
//     Static singleton cache of solution file contents for
//     solution-wide InlineHints reference counting and Find All References.
//     Subscribes to SolutionManager.Instance.SolutionChanged to
//     invalidate on every solution open / close / reload.
//
// Architecture Notes:
//     Pattern: Cache / Repository.
//     Thread-safe: ReaderWriterLockSlim allows concurrent reads from
//     multiple Task.Run background workers while writes are exclusive.
//     Caps: max 500 files, max 2 MB per file, to bound memory usage.
// ==========================================================

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Core.ProjectSystem.Services;

namespace WpfHexEditor.Editor.CodeEditor.Services;

internal static class WorkspaceFileCache
{
    // ── Constants ─────────────────────────────────────────────────────────────

    private const int  MaxFiles     = 5_000;
    private const long MaxFileBytes = 2L * 1024 * 1024; // 2 MB

    // ── State ─────────────────────────────────────────────────────────────────

    private static readonly ReaderWriterLockSlim                  _lock         = new();
    private static readonly Dictionary<string, string[]>          _lineCache    = new(StringComparer.OrdinalIgnoreCase);
    private static          IReadOnlyList<string>                  _solutionPaths = [];

    // ── Initialisation ────────────────────────────────────────────────────────

    static WorkspaceFileCache()
    {
        // Invalidate whenever the solution is opened, closed, or reloaded.
        SolutionManager.Instance.SolutionChanged += (_, _) => Invalidate();
    }

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Raised (on an arbitrary thread) after the workspace cache has been
    /// invalidated — i.e., when the solution opens, closes, or reloads.
    /// Subscribers that touch WPF objects must marshal back to the UI thread.
    /// </summary>
    internal static event Action? WorkspaceChanged;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns all solution file paths whose extension matches any entry in
    /// <paramref name="extensions"/>.  Fast read-locked access.
    /// </summary>
    internal static IReadOnlyList<string> GetPathsForExtensions(IEnumerable<string> extensions)
    {
        var exts = new HashSet<string>(extensions, StringComparer.OrdinalIgnoreCase);

        _lock.EnterReadLock();
        try
        {
            return _solutionPaths
                .Where(p => exts.Contains(Path.GetExtension(p)))
                .ToList();
        }
        finally { _lock.ExitReadLock(); }
    }

    /// <summary>
    /// Returns the line array for <paramref name="path"/>, reading from disk on
    /// the first access and caching the result for subsequent callers.
    /// Returns <see langword="null"/> when the file cannot be read, does not
    /// exist, or exceeds the size cap.
    /// </summary>
    internal static string[]? GetLines(string path)
    {
        // Fast path — check cache under read lock.
        _lock.EnterReadLock();
        try
        {
            if (_lineCache.TryGetValue(path, out var cached))
                return cached;
        }
        finally { _lock.ExitReadLock(); }

        // Slow path — read from disk outside any lock.
        if (!File.Exists(path)) return null;

        try
        {
            var info = new FileInfo(path);
            if (info.Length > MaxFileBytes) return null;
        }
        catch { return null; }

        string[] lines;
        try { lines = File.ReadAllLines(path); }
        catch { return null; }

        // Store in cache under write lock; TryAdd is idempotent if a
        // concurrent reader already stored the same path.
        _lock.EnterWriteLock();
        try { _lineCache.TryAdd(path, lines); }
        finally { _lock.ExitWriteLock(); }

        return lines;
    }

    /// <summary>
    /// Clears the line cache and rebuilds the solution path list from
    /// <see cref="SolutionManager.Instance"/>.
    /// </summary>
    internal static void Invalidate()
    {
        _lock.EnterWriteLock();
        try
        {
            _lineCache.Clear();
            _solutionPaths = BuildSolutionPaths();
        }
        finally { _lock.ExitWriteLock(); }

        // Notify subscribers outside the lock to prevent deadlocks.
        WorkspaceChanged?.Invoke();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static IReadOnlyList<string> BuildSolutionPaths()
    {
        var solution = SolutionManager.Instance.CurrentSolution;
        if (solution is null) return [];

        var paths = new List<string>();

        foreach (var project in solution.Projects)
        foreach (var item in project.Items)
        {
            var path = item.AbsolutePath;
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                paths.Add(path);
                if (paths.Count >= MaxFiles) return paths;
            }
        }

        return paths;
    }
}
