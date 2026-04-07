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

using System.IO;
using System.Windows;
using System.Windows.Threading;
using WpfHexEditor.Editor.ClassDiagram.Core.Model;
using WpfHexEditor.Plugins.ClassDiagram.Options;

namespace WpfHexEditor.Plugins.ClassDiagram.Analysis;

/// <summary>
/// Watches a set of C# source files and fires <see cref="DocumentPatched"/>
/// when any of them change on disk. Updates are debounced by 800 ms.
/// </summary>
public sealed class DiagramLiveSyncService : IDisposable
{
    private readonly IReadOnlyList<string>      _filePaths;
    private readonly ClassDiagramOptions        _options;
    private readonly List<FileSystemWatcher>    _watchers  = [];
    private          DiagramDocument            _current;
    private          System.Threading.Timer?    _debounce;
    private          bool                       _disposed;

    private const int DebounceMs = 800;

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

        DiagramDocument next;
        try
        {
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
