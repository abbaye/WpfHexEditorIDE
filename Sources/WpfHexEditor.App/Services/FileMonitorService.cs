
//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using WpfHexEditor.Editor.Core;
using Timer = System.Timers.Timer;

namespace WpfHexEditor.App.Services;

/// <summary>
/// Watches project directories for file changes and runs background validation
/// via any <see cref="IFileValidator"/>-implementing factory registered in the
/// <see cref="IEditorRegistry"/>.
///
/// <para>Results are pushed into a <see cref="FileMonitorDiagnosticSource"/> which
/// is registered permanently in the Error Panel.</para>
///
/// <para>Uses per-file debouncing (800 ms) to avoid redundant validation during rapid
/// saves or IDE auto-saves. Validation runs at <c>ThreadPriority.BelowNormal</c>
/// to avoid blocking the UI.</para>
/// </summary>
public sealed class FileMonitorService : IDisposable
{
    // Extensions that have a registered IFileValidator factory
    private static readonly HashSet<string> WatchedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".tbl", ".tblx" };

    // .whchg companion files — no validation, just existence notification
    private const string ChangesetExtension = ".whchg";

    /// <summary>
    /// Raised (on a background thread, debounced 800 ms) when a <c>.whchg</c>
    /// companion file is created, modified, or deleted.
    /// <c>exists</c> is false when the file was deleted.
    /// </summary>
    public event Action<string, bool>? ChangesetFileChanged;

    private readonly IEditorRegistry                    _registry;
    private readonly FileMonitorDiagnosticSource        _source;
    private readonly List<FileSystemWatcher>            _watchers    = [];
    private readonly Dictionary<string, Timer>          _debounce    = [];
    private readonly object                             _debounceLock = new();
    private          CancellationTokenSource?           _cts;
    private          bool                               _disposed;

    private const double DebounceMs = 800;

    public FileMonitorService(IEditorRegistry registry, FileMonitorDiagnosticSource source)
    {
        _registry = registry;
        _source   = source;
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────

    /// <summary>
    /// Starts watching <paramref name="directories"/> (one per project root).
    /// Any previously watched paths are replaced.
    /// </summary>
    public void StartWatching(IEnumerable<string> directories)
    {
        StopWatching();

        _cts = new CancellationTokenSource();
        foreach (var dir in directories.Where(Directory.Exists))
            AddWatcher(dir);
    }

    /// <summary>Stops all watchers and cancels any pending validations.</summary>
    public void StopWatching()
    {
        _cts?.Cancel();

        lock (_debounceLock)
        {
            foreach (var t in _debounce.Values) { t.Stop(); t.Dispose(); }
            _debounce.Clear();
        }

        foreach (var w in _watchers) { w.EnableRaisingEvents = false; w.Dispose(); }
        _watchers.Clear();

        _source.Clear();
    }

    // ── Internal ──────────────────────────────────────────────────────────

    private void AddWatcher(string directory)
    {
        var watcher = new FileSystemWatcher(directory)
        {
            IncludeSubdirectories = true,
            NotifyFilter          = NotifyFilters.LastWrite | NotifyFilters.FileName,
            EnableRaisingEvents   = true
        };

        watcher.Changed += OnFileEvent;
        watcher.Created += OnFileEvent;
        watcher.Deleted += OnFileDeleted;
        watcher.Renamed += OnFileRenamed;
        watcher.Error   += OnWatcherError;

        _watchers.Add(watcher);
    }

    private void OnFileEvent(object sender, FileSystemEventArgs e)
    {
        if (IsChangeset(e.FullPath))
        {
            ScheduleChangesetNotify(e.FullPath, exists: true);
            return;
        }
        if (!IsWatched(e.FullPath)) return;
        ScheduleValidation(e.FullPath);
    }

    private void OnFileDeleted(object sender, FileSystemEventArgs e)
    {
        if (IsChangeset(e.FullPath))
        {
            ScheduleChangesetNotify(e.FullPath, exists: false);
            return;
        }
        if (!IsWatched(e.FullPath)) return;
        _source.RemoveFile(e.FullPath);
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        if (IsChangeset(e.OldFullPath)) ScheduleChangesetNotify(e.OldFullPath, exists: false);
        if (IsChangeset(e.FullPath))    ScheduleChangesetNotify(e.FullPath,    exists: true);
        if (IsWatched(e.OldFullPath))   _source.RemoveFile(e.OldFullPath);
        if (IsWatched(e.FullPath))      ScheduleValidation(e.FullPath);
    }

    private static void OnWatcherError(object sender, ErrorEventArgs e)
        => OutputLogger.Warn($"FileMonitor watcher error: {e.GetException().Message}");

    private void ScheduleValidation(string filePath)
    {
        lock (_debounceLock)
        {
            if (_debounce.TryGetValue(filePath, out var existing))
            {
                existing.Stop();
                existing.Start();
                return;
            }

            var timer = new Timer(DebounceMs) { AutoReset = false };
            timer.Elapsed += (_, _) => OnDebounceElapsed(filePath);
            _debounce[filePath] = timer;
            timer.Start();
        }
    }

    private void OnDebounceElapsed(string filePath)
    {
        lock (_debounceLock)
        {
            if (_debounce.TryGetValue(filePath, out var t)) { t.Dispose(); _debounce.Remove(filePath); }
        }

        var ct = _cts?.Token ?? CancellationToken.None;
        if (ct.IsCancellationRequested) return;

        _ = ValidateInBackgroundAsync(filePath, ct);
    }

    private async Task ValidateInBackgroundAsync(string filePath, CancellationToken ct)
    {
        try
        {
            var factory = _registry.FindFactory(filePath);
            if (factory is not IFileValidator validator) return;

            // Run at below-normal priority — don't compete with UI
            var diagnostics = await Task.Factory.StartNew(
                () => validator.ValidateAsync(filePath, ct).GetAwaiter().GetResult(),
                ct,
                TaskCreationOptions.None,
                PriorityScheduler.BelowNormal);

            if (!ct.IsCancellationRequested)
                _source.UpdateFile(filePath, diagnostics);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            OutputLogger.Warn($"FileMonitor validation error ({Path.GetFileName(filePath)}): {ex.Message}");
        }
    }

    private static bool IsWatched(string path)
        => WatchedExtensions.Contains(Path.GetExtension(path));

    private static bool IsChangeset(string path)
        => path.EndsWith(ChangesetExtension, StringComparison.OrdinalIgnoreCase);

    private void ScheduleChangesetNotify(string filePath, bool exists)
    {
        // Reuse the same debounce mechanism as validation
        var key = filePath + (exists ? ":exists" : ":deleted");
        lock (_debounceLock)
        {
            if (_debounce.TryGetValue(key, out var existing))
            {
                existing.Stop();
                existing.Start();
                return;
            }
            var timer = new Timer(DebounceMs) { AutoReset = false };
            timer.Elapsed += (_, _) =>
            {
                lock (_debounceLock)
                {
                    if (_debounce.TryGetValue(key, out var t)) { t.Dispose(); _debounce.Remove(key); }
                }
                ChangesetFileChanged?.Invoke(filePath, exists);
            };
            _debounce[key] = timer;
            timer.Start();
        }
    }

    // ── IDisposable ───────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopWatching();
        _cts?.Dispose();
    }
}

// ── Below-normal priority TaskScheduler ───────────────────────────────────

/// <summary>
/// Simple TaskScheduler that runs tasks on a ThreadPool thread with below-normal
/// thread priority, keeping background validation from interfering with the UI.
/// </summary>
file sealed class PriorityScheduler : TaskScheduler
{
    public static readonly PriorityScheduler BelowNormal =
        new(ThreadPriority.BelowNormal);

    private readonly ThreadPriority _priority;

    private PriorityScheduler(ThreadPriority priority) => _priority = priority;

    protected override IEnumerable<Task> GetScheduledTasks() => [];

    protected override void QueueTask(Task task)
    {
        var thread = new Thread(() => TryExecuteTask(task))
        {
            IsBackground = true,
            Priority     = _priority
        };
        thread.Start();
    }

    protected override bool TryExecuteTaskInline(Task task, bool previouslyQueued) => false;
}
