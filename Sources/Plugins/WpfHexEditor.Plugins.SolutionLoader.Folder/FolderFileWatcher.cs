// ==========================================================
// Project: WpfHexEditor.Plugins.SolutionLoader.Folder
// File: FolderFileWatcher.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-17
// Description:
//     Wraps FileSystemWatcher on the opened folder root.
//     Debounces filesystem events (500 ms) and triggers an async reload
//     of the folder solution via ISolutionManager.LoadExternalSolutionAsync.
//     Disposed automatically when a new folder is opened or the plugin shuts down.
//
// Architecture Notes:
//     Pattern: Observer (FileSystemWatcher) + Debounce (Timer)
//     Holds a weak reference to IIDEHostContext to avoid keeping the IDE
//     host alive if the plugin is unloaded before the timer fires.
// ==========================================================

using WpfHexEditor.Editor.Core;
using WpfHexEditor.SDK.Contracts;

namespace WpfHexEditor.Plugins.SolutionLoader.Folder;

/// <summary>
/// Monitors a folder for file system changes and triggers a solution reload
/// after a 500 ms debounce period.
/// </summary>
internal sealed class FolderFileWatcher : IDisposable
{
    private const int DebounceMs = 500;

    private readonly string               _rootDir;
    private readonly string               _markerPath;
    private readonly WeakReference<IIDEHostContext> _contextRef;
    private readonly FileSystemWatcher    _watcher;
    private readonly Timer                _debounceTimer;

    private bool _disposed;

    // -----------------------------------------------------------------------
    // Constructor
    // -----------------------------------------------------------------------

    public FolderFileWatcher(string rootDir, string markerPath, IIDEHostContext context)
    {
        _rootDir    = rootDir;
        _markerPath = markerPath;
        _contextRef = new WeakReference<IIDEHostContext>(context);

        _debounceTimer = new Timer(OnDebounceElapsed, null, Timeout.Infinite, Timeout.Infinite);

        _watcher = new FileSystemWatcher(rootDir)
        {
            NotifyFilter          = NotifyFilters.FileName
                                  | NotifyFilters.DirectoryName
                                  | NotifyFilters.LastWrite,
            IncludeSubdirectories = true,
            EnableRaisingEvents   = true,
        };

        _watcher.Created += OnFsEvent;
        _watcher.Deleted += OnFsEvent;
        _watcher.Renamed += OnFsRenamed;
    }

    // -----------------------------------------------------------------------
    // Event handlers
    // -----------------------------------------------------------------------

    private void OnFsEvent(object sender, FileSystemEventArgs e) => ResetDebounce();

    private void OnFsRenamed(object sender, RenamedEventArgs e) => ResetDebounce();

    private void ResetDebounce()
    {
        if (_disposed) return;
        // Reset the timer; fires once after DebounceMs of quiet.
        _debounceTimer.Change(DebounceMs, Timeout.Infinite);
    }

    private void OnDebounceElapsed(object? state)
    {
        if (_disposed) return;
        if (!_contextRef.TryGetTarget(out var context)) return;

        // Fire-and-forget; ISolutionManager.LoadExternalSolutionAsync is safe to call
        // from a background thread as long as it marshals UI updates internally.
        _ = ReloadAsync(context);
    }

    private async Task ReloadAsync(IIDEHostContext context)
    {
        try
        {
            var loader   = context.ExtensionRegistry
                               .GetExtensions<ISolutionLoader>()
                               .FirstOrDefault(l => l.CanLoad(_markerPath));
            if (loader is null) return;

            var solution = await loader.LoadAsync(_markerPath).ConfigureAwait(false);
            await context.SolutionManager
                         .LoadExternalSolutionAsync(solution, _markerPath)
                         .ConfigureAwait(false);
        }
        catch
        {
            // Swallow — a transient file system event must not crash the watcher loop.
        }
    }

    // -----------------------------------------------------------------------
    // IDisposable
    // -----------------------------------------------------------------------

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _watcher.EnableRaisingEvents = false;
        _watcher.Created -= OnFsEvent;
        _watcher.Deleted -= OnFsEvent;
        _watcher.Renamed -= OnFsRenamed;
        _watcher.Dispose();
        _debounceTimer.Dispose();
    }
}
