// ==========================================================
// Project: WpfHexEditor.Plugins.ClassDiagram
// File: Analysis/DiagramLiveSyncService.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-04-07
// Description:
//     Live-sync service that watches a set of source files with
//     FileSystemWatcher and, on any change, debounces 800 ms then
//     re-runs RoslynClassDiagramAnalyzer and fires DocumentPatched
//     with a DiagramPatch so the canvas can update incrementally.
//
// Architecture Notes:
//     Pattern: Observer — callers subscribe DocumentPatched.
//     Owns one FileSystemWatcher per unique directory in the file set.
//     Debouncing is done with a System.Threading.Timer (reset on each
//     change event before the interval elapses).
//     Dispatches DocumentPatched on the WPF Dispatcher so callers
//     can safely update UI without extra marshalling.
// ==========================================================

using System.Collections.Concurrent;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using WpfHexEditor.Editor.ClassDiagram.Core.Model;
using WpfHexEditor.Editor.ClassDiagram.Services;
using WpfHexEditor.Plugins.ClassDiagram.Options;

namespace WpfHexEditor.Plugins.ClassDiagram.Analysis;

/// <summary>
/// Watches a set of C# source files and fires <see cref="DocumentPatched"/>
/// when any of them change on disk. Updates are debounced by 800 ms.
/// Implements ADR-021 cycle-prevention: file paths registered via
/// <see cref="SuppressNextChange"/> drop incoming FSW events until their
/// per-file suppression window elapses.
/// </summary>
public sealed class DiagramLiveSyncService : IDisposable, ILiveSyncCoordinator
{
    private readonly IReadOnlyList<string>                  _filePaths;
    private readonly ClassDiagramOptions                    _options;
    private readonly List<FileSystemWatcher>                _watchers      = [];
    private readonly ConcurrentDictionary<string, DateTime> _suppressUntil =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentBag<string>                  _pendingChanges = [];
    private          DiagramDocument                        _current;
    private          System.Threading.Timer?                _debounce;
    private          bool                                   _disposed;

    private const int DebounceMs        = 800;
    private const int SuppressionMs     = 500;

    /// <summary>
    /// Fired on the WPF Dispatcher thread when a live-sync cycle completes.
    /// </summary>
    public event EventHandler<DiagramPatchEventArgs>? DocumentPatched;

    // ── Construction ─────────────────────────────────────────────────────────

    public DiagramLiveSyncService(
        IEnumerable<string>    filePaths,
        DiagramDocument        initialDocument,
        ClassDiagramOptions    options)
    {
        _filePaths = filePaths.ToList();
        _current   = initialDocument;
        _options   = options;

        StartWatchers();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Replaces the baseline document used for diffing (e.g. after a manual re-layout).</summary>
    public void UpdateBaseline(DiagramDocument doc) => _current = doc;

    /// <summary>
    /// ADR-021 cycle prevention. Round-trip writers (DiagramCodeEditService and
    /// implementations of ILanguageRoundTripEditor) must call this BEFORE writing
    /// to a source file. Any FSW Changed event for that path arriving within the
    /// suppression window is dropped instead of triggering a sync cycle.
    /// </summary>
    /// <param name="filePath">Absolute path of the file about to be written.</param>
    /// <param name="windowMs">Optional override; defaults to 500 ms.</param>
    public void SuppressNextChange(string filePath, int? windowMs = null)
    {
        if (string.IsNullOrEmpty(filePath)) return;
        var deadline = DateTime.UtcNow.AddMilliseconds(windowMs ?? SuppressionMs);
        _suppressUntil[filePath] = deadline;
    }

    /// <summary>True if the given path is currently in its suppression window.</summary>
    internal bool IsSuppressed(string filePath)
    {
        if (!_suppressUntil.TryGetValue(filePath, out var deadline)) return false;
        if (DateTime.UtcNow >= deadline)
        {
            _suppressUntil.TryRemove(filePath, out _);
            return false;
        }
        return true;
    }

    // ── FSW setup ─────────────────────────────────────────────────────────────

    private void StartWatchers()
    {
        var directories = _filePaths
            .Select(Path.GetDirectoryName)
            .Where(d => !string.IsNullOrEmpty(d))
            .Distinct(StringComparer.OrdinalIgnoreCase)!;

        foreach (string dir in directories)
        {
            var watcher = new FileSystemWatcher(dir, "*.cs")
            {
                NotifyFilter           = NotifyFilters.LastWrite | NotifyFilters.Size,
                IncludeSubdirectories  = false,
                EnableRaisingEvents    = true
            };

            watcher.Changed += OnFileEvent;
            _watchers.Add(watcher);
        }
    }

    // ── Change handling ───────────────────────────────────────────────────────

    private void OnFileEvent(object sender, FileSystemEventArgs e)
    {
        if (!_filePaths.Contains(e.FullPath, StringComparer.OrdinalIgnoreCase))
            return;

        // ADR-021: drop events for files inside their suppression window
        // (writes originated by our own round-trip code, not user edits).
        if (IsSuppressed(e.FullPath))
            return;

        // Accumulate the changed path for incremental analysis.
        _pendingChanges.Add(e.FullPath);

        // Reset the debounce timer on every incoming event.
        _debounce?.Dispose();
        _debounce = new System.Threading.Timer(
            _ => RunSyncCycle(),
            null,
            DebounceMs,
            System.Threading.Timeout.Infinite);
    }

    private void RunSyncCycle()
    {
        if (_disposed) return;

        // Drain the pending-change bag before analysis begins.
        var changed = new List<string>();
        while (_pendingChanges.TryTake(out var path))
            changed.Add(path);

        var changedDistinct = changed
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(p => _filePaths.Contains(p, StringComparer.OrdinalIgnoreCase))
            .ToList();

        DiagramDocument next;
        try
        {
            // Incremental path: only the files that actually changed need re-parsing.
            // Fall back to full analysis when every file is affected or changed set is empty.
            if (changedDistinct.Count > 0 && changedDistinct.Count < _filePaths.Count)
                next = RoslynClassDiagramAnalyzer.AnalyzeFilesIncremental(
                    _current, _filePaths, changedDistinct, _options);
            else
                next = _filePaths.Count == 1
                    ? RoslynClassDiagramAnalyzer.AnalyzeFile(_filePaths[0], _options)
                    : RoslynClassDiagramAnalyzer.AnalyzeFiles([.. _filePaths], _options);
        }
        catch
        {
            // Parse error — skip this cycle; next save will retry.
            return;
        }

        var patch = DiagramPatch.Diff(_current, next);
        if (patch.IsEmpty) return;

        _current = next;

        Application.Current?.Dispatcher.BeginInvoke(
            DispatcherPriority.Background,
            () => DocumentPatched?.Invoke(this, new DiagramPatchEventArgs(patch, next)));
    }

    // ── IDisposable ───────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _debounce?.Dispose();
        _debounce = null;

        foreach (var w in _watchers)
        {
            w.EnableRaisingEvents = false;
            w.Dispose();
        }

        _watchers.Clear();
    }
}

/// <summary>Event args for <see cref="DiagramLiveSyncService.DocumentPatched"/>.</summary>
public sealed class DiagramPatchEventArgs : EventArgs
{
    public DiagramPatch    Patch    { get; }
    public DiagramDocument Document { get; }

    public DiagramPatchEventArgs(DiagramPatch patch, DiagramDocument document)
    {
        Patch    = patch;
        Document = document;
    }
}
