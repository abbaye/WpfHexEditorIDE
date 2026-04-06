// ==========================================================
// Project: WpfHexEditor.Shell.Panels
// File: Services/SolutionFileWatcher.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-05
// Description:
//     Watches the physical directories of all projects in the active
//     solution for external file changes (saves, renames, deletes).
//     Debounces noisy file-system events (500 ms) before raising
//     FileChangedExternally so callers can prompt the user to reload.
//
// Architecture Notes:
//     Observer Pattern — ISolution is observed; one FileSystemWatcher
//     per unique project root directory.
//     Debounce  — ConcurrentDictionary<path, Timer> prevents event flood.
//     Cleanup   — IDisposable; dispose all watchers on solution close.
// ==========================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.Shell.Panels.Services;

/// <summary>
/// Watches all project directories for external file-system changes and
/// raises <see cref="FileChangedExternally"/> for every affected <see cref="IProjectItem"/>.
/// </summary>
public sealed class SolutionFileWatcher : IDisposable
{
    private const int DebounceMs = 500;

    private readonly List<FileSystemWatcher>                  _watchers    = [];
    private readonly ConcurrentDictionary<string, System.Threading.Timer> _debounce  = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IProjectItem>         _itemIndex   = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte>       _suppressed  = new(StringComparer.OrdinalIgnoreCase);
    private FileSystemWatcher?                                _slnWatcher;

    // Configurable ignored directory names (set via SetIgnoredDirectories).
    private volatile string[] _ignoredDirNames = ["bin", "obj", ".vs", ".git", "node_modules"];

    // The solution currently being watched (null when disposed / no solution loaded).
    private ISolution? _solution;

    // -- Public API -----------------------------------------------------------

    /// <summary>
    /// Raised on the thread-pool (marshal to UI thread as needed) when a
    /// project file has been modified, renamed, or deleted externally.
    /// </summary>
    public event EventHandler<FileChangedExternallyEventArgs>? FileChangedExternally;

    /// <summary>
    /// Raised when the solution file itself (.sln, .slnx, .whsln, etc.) has been
    /// modified externally (e.g. a project was added/removed in Visual Studio).
    /// Callers should prompt the user to reload the solution.
    /// </summary>
    public event EventHandler<FileChangedExternallyEventArgs>? SolutionFileChangedExternally;

    /// <summary>
    /// Temporarily suppresses change notifications for <paramref name="fullPath"/>.
    /// Use when the IDE itself is saving a file to avoid false-positive external-modification warnings.
    /// The suppression auto-expires after the debounce window.
    /// </summary>
    public void SuppressPath(string fullPath)
    {
        _suppressed[fullPath] = 0;

        // Auto-expire after debounce + margin so the timer callback sees the suppression.
        _ = new System.Threading.Timer(
            state =>
            {
                var (dict, p) = ((ConcurrentDictionary<string, byte>, string))state!;
                dict.TryRemove(p, out byte _v);
            },
            (_suppressed, fullPath),
            DebounceMs + 200,
            System.Threading.Timeout.Infinite);
    }

    /// <summary>
    /// Updates the list of ignored directory names from a semicolon-separated string.
    /// </summary>
    public void SetIgnoredDirectories(string? semicolonSeparated)
    {
        if (string.IsNullOrWhiteSpace(semicolonSeparated))
        {
            _ignoredDirNames = [];
            return;
        }
        var parts = semicolonSeparated.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        _ignoredDirNames = parts;
    }

    /// <summary>
    /// Begins watching all projects in <paramref name="solution"/>.
    /// Any previously watched solution is stopped first.
    /// </summary>
    public void Watch(ISolution solution)
    {
        StopWatchers();
        _solution = solution;
        RebuildIndex(solution);
        StartWatchers(solution);
    }

    /// <summary>
    /// Stops all file-system watchers and clears the item index.
    /// </summary>
    public void Stop()
    {
        StopWatchers();
        _solution = null;
    }

    /// <summary>
    /// Re-starts watchers using the previously set solution (if any).
    /// Useful after changing ignored directories or re-enabling detection.
    /// </summary>
    public void RestartIfWatching()
    {
        if (_solution is not null)
            Watch(_solution);
    }

    // -- Index construction ---------------------------------------------------

    private void RebuildIndex(ISolution solution)
    {
        _itemIndex.Clear();

        foreach (var project in solution.Projects)
        {
            foreach (var item in project.Items)
            {
                if (!string.IsNullOrEmpty(item.AbsolutePath))
                    _itemIndex[item.AbsolutePath] = item;
            }
        }
    }

    // -- FileSystemWatcher management -----------------------------------------

    private void StartWatchers(ISolution solution)
    {
        // Watch the solution file itself for external modifications.
        if (!string.IsNullOrEmpty(solution.FilePath) && File.Exists(solution.FilePath))
        {
            var slnDir  = Path.GetDirectoryName(solution.FilePath)!;
            var slnName = Path.GetFileName(solution.FilePath);
            _slnWatcher = new FileSystemWatcher(slnDir, slnName)
            {
                NotifyFilter        = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };
            _slnWatcher.Changed += OnSolutionFileChanged;
        }

        // Watch project directories for file-level changes.
        var watchedDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var project in solution.Projects)
        {
            var dir = Path.GetDirectoryName(project.ProjectFilePath);
            if (dir is null || !Directory.Exists(dir) || !watchedDirs.Add(dir))
                continue;

            var watcher = new FileSystemWatcher(dir)
            {
                IncludeSubdirectories  = true,
                NotifyFilter           = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                EnableRaisingEvents    = true
            };

            watcher.Changed += OnFsEvent;
            watcher.Renamed += OnFsRenamed;
            watcher.Deleted += OnFsEvent;

            _watchers.Add(watcher);
        }
    }

    private void StopWatchers()
    {
        if (_slnWatcher is not null)
        {
            _slnWatcher.EnableRaisingEvents = false;
            _slnWatcher.Dispose();
            _slnWatcher = null;
        }

        foreach (var w in _watchers)
        {
            w.EnableRaisingEvents = false;
            w.Dispose();
        }
        _watchers.Clear();

        // Cancel pending debounce timers.
        foreach (var timer in _debounce.Values)
            timer.Dispose();
        _debounce.Clear();
    }

    // -- Event handlers -------------------------------------------------------

    private void OnSolutionFileChanged(object sender, FileSystemEventArgs e)
    {
        // Debounce the solution-file change like regular project files.
        var key = "::sln::" + e.FullPath;
        _debounce.AddOrUpdate(
            key,
            _ => new System.Threading.Timer(_ =>
            {
                _debounce.TryRemove(key, out var t);
                t?.Dispose();
                if (_suppressed.TryRemove(e.FullPath, out byte _s)) return;
                SolutionFileChangedExternally?.Invoke(this,
                    new FileChangedExternallyEventArgs(e.FullPath, e.ChangeType, item: null));
            }, null, DebounceMs, System.Threading.Timeout.Infinite),
            (_, existing) =>
            {
                existing.Change(DebounceMs, System.Threading.Timeout.Infinite);
                return existing;
            });
    }

    private void OnFsEvent(object sender, FileSystemEventArgs e)
        => ScheduleNotification(e.FullPath, e.ChangeType);

    private void OnFsRenamed(object sender, RenamedEventArgs e)
    {
        // Notify for both old (deleted) and new (created) paths.
        ScheduleNotification(e.OldFullPath, WatcherChangeTypes.Deleted);
        ScheduleNotification(e.FullPath,    WatcherChangeTypes.Renamed);
    }

    private void ScheduleNotification(string fullPath, WatcherChangeTypes changeType)
    {
        if (IsIgnoredPath(fullPath)) return;

        // Debounce: reset the timer every time a new event arrives for the same path.
        _debounce.AddOrUpdate(
            fullPath,
            _ => CreateTimer(fullPath, changeType),
            (_, existing) =>
            {
                existing.Change(DebounceMs, System.Threading.Timeout.Infinite);
                return existing;
            });
    }

    private System.Threading.Timer CreateTimer(string fullPath, WatcherChangeTypes changeType)
    {
        return new System.Threading.Timer(_ =>
        {
            _debounce.TryRemove(fullPath, out var t);
            t?.Dispose();

            // Skip if the IDE itself triggered this change (e.g. save operation).
            if (_suppressed.TryRemove(fullPath, out byte _s)) return;

            // Find the matching project item (if any).
            _itemIndex.TryGetValue(fullPath, out var item);

            FileChangedExternally?.Invoke(this, new FileChangedExternallyEventArgs(fullPath, changeType, item));

        }, null, DebounceMs, System.Threading.Timeout.Infinite);
    }

    // -- Path filtering -------------------------------------------------------

    private bool IsIgnoredPath(string fullPath)
    {
        var sep = Path.DirectorySeparatorChar;
        foreach (var dir in _ignoredDirNames)
        {
            if (fullPath.Contains($"{sep}{dir}{sep}", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    // -- IDisposable ----------------------------------------------------------

    public void Dispose() => StopWatchers();
}

/// <summary>
/// Event arguments for <see cref="SolutionFileWatcher.FileChangedExternally"/>.
/// </summary>
public sealed class FileChangedExternallyEventArgs : EventArgs
{
    public FileChangedExternallyEventArgs(
        string fullPath,
        WatcherChangeTypes changeType,
        IProjectItem? item)
    {
        FullPath   = fullPath;
        ChangeType = changeType;
        Item       = item;
    }

    /// <summary>Absolute path of the changed file.</summary>
    public string FullPath { get; }

    /// <summary>Type of file-system change detected.</summary>
    public WatcherChangeTypes ChangeType { get; }

    /// <summary>
    /// The matching project item, or <see langword="null"/> if the path is not
    /// tracked by any project (e.g. a new file created externally).
    /// </summary>
    public IProjectItem? Item { get; }
}
