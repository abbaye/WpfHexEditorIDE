// ==========================================================
// Project: WpfHexEditor.BuildSystem
// File: BuildFileWatcher.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Description:
//     Creates one FileSystemWatcher per registered project directory and
//     invalidates the corresponding project in IIncrementalBuildTracker
//     when a tracked source file changes.
//
// Architecture Notes:
//     Tracked extensions: .cs .vb .fs .csproj .vbproj .fsproj .resx .xaml .json
//     FSW events fire on a ThreadPool thread — IncrementalBuildTracker.Invalidate
//     is thread-safe (lock-based).
//     IncludeSubdirectories=true covers nested source folders (Properties/, Models/, …).
// ==========================================================

namespace WpfHexEditor.Core.BuildSystem;

/// <summary>
/// Watches project source directories and marks projects dirty via
/// <see cref="IIncrementalBuildTracker"/> when tracked files change.
/// </summary>
public sealed class BuildFileWatcher : IDisposable
{
    private static readonly HashSet<string> TrackedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".vb", ".fs",
        ".csproj", ".vbproj", ".fsproj",
        ".resx", ".xaml", ".json",
    };

    private readonly IIncrementalBuildTracker                  _tracker;
    private readonly Dictionary<string, FileSystemWatcher>     _watchers
        = new(StringComparer.OrdinalIgnoreCase);

    private bool _disposed;

    // -----------------------------------------------------------------------

    public BuildFileWatcher(IIncrementalBuildTracker tracker)
        => _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));

    // -----------------------------------------------------------------------

    /// <summary>
    /// Registers a project and starts watching its containing directory.
    /// Idempotent — calling with the same <paramref name="projectId"/> twice is safe.
    /// </summary>
    public void Watch(string projectId, string projectFilePath)
    {
        if (_disposed || string.IsNullOrEmpty(projectFilePath)) return;
        if (_watchers.ContainsKey(projectId)) return;

        var dir = Path.GetDirectoryName(projectFilePath);
        if (dir is null || !Directory.Exists(dir)) return;

        var fsw = new FileSystemWatcher(dir)
        {
            IncludeSubdirectories = true,
            NotifyFilter          = NotifyFilters.LastWrite | NotifyFilters.FileName,
            EnableRaisingEvents   = true,
        };

        void OnChange(object _, FileSystemEventArgs e)
        {
            if (IsTracked(e.Name))
                _tracker.Invalidate(projectId);
        }

        fsw.Changed += OnChange;
        fsw.Created += OnChange;
        fsw.Deleted += OnChange;
        fsw.Renamed += (_, e) =>
        {
            if (IsTracked(e.Name) || IsTracked(e.OldName))
                _tracker.Invalidate(projectId);
        };

        _watchers[projectId] = fsw;
    }

    /// <summary>Stops watching the given project directory.</summary>
    public void Unwatch(string projectId)
    {
        if (_watchers.Remove(projectId, out var fsw))
        {
            fsw.EnableRaisingEvents = false;
            fsw.Dispose();
        }
    }

    // -----------------------------------------------------------------------

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var fsw in _watchers.Values)
        {
            fsw.EnableRaisingEvents = false;
            fsw.Dispose();
        }
        _watchers.Clear();
    }

    // -----------------------------------------------------------------------

    private static bool IsTracked(string? name)
        => name is not null
           && TrackedExtensions.Contains(Path.GetExtension(name));
}
