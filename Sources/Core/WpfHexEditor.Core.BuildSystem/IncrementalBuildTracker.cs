// ==========================================================
// Project: WpfHexEditor.BuildSystem
// File: IncrementalBuildTracker.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Description:
//     Timestamp-based implementation of IIncrementalBuildTracker.
//     Stores the UTC tick count at last successful build per project.
//     Projects with no snapshot are considered dirty by default.
//
// Architecture Notes:
//     Thread-safe via lock on _lock (Dictionary reads + writes).
//     Also publishes ProjectDirtyChangedEvent to IIDEEventBus so that
//     the Solution Explorer panel can subscribe without a direct reference
//     to this assembly.
// ==========================================================

using WpfHexEditor.Core.Events;
using WpfHexEditor.Core.Events.IDEEvents;

namespace WpfHexEditor.Core.BuildSystem;

/// <summary>
/// Timestamp-based <see cref="IIncrementalBuildTracker"/> implementation.
/// </summary>
public sealed class IncrementalBuildTracker : IIncrementalBuildTracker
{
    // projectId → UTC ticks at last successful build
    private readonly Dictionary<string, long> _snapshots
        = new(StringComparer.OrdinalIgnoreCase);

    // projectIds currently dirty
    private readonly HashSet<string> _dirty
        = new(StringComparer.OrdinalIgnoreCase);

    private readonly IIDEEventBus _eventBus;
    private readonly object       _lock = new();

    // -----------------------------------------------------------------------

    public IncrementalBuildTracker(IIDEEventBus eventBus)
        => _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));

    // -----------------------------------------------------------------------
    // IIncrementalBuildTracker
    // -----------------------------------------------------------------------

    public event EventHandler<ProjectDirtyChangedEventArgs>? DirtyChanged;

    public bool IsProjectDirty(string projectId)
    {
        lock (_lock)
            return _dirty.Contains(projectId) || !_snapshots.ContainsKey(projectId);
    }

    public IReadOnlyList<string> GetDirtyProjects(IEnumerable<string> projectIds)
        => projectIds.Where(IsProjectDirty).ToList();

    public void RecordSuccess(string projectId)
    {
        bool changed;
        lock (_lock)
        {
            changed = _dirty.Remove(projectId);
            _snapshots[projectId] = DateTimeOffset.UtcNow.Ticks;
        }

        if (changed)
            RaiseChanged(projectId, isDirty: false);
    }

    public void Invalidate(string projectId)
    {
        bool changed;
        lock (_lock)
            changed = _dirty.Add(projectId);

        if (changed)
            RaiseChanged(projectId, isDirty: true);
    }

    // -----------------------------------------------------------------------

    private void RaiseChanged(string projectId, bool isDirty)
    {
        var args = new ProjectDirtyChangedEventArgs { ProjectId = projectId, IsDirty = isDirty };
        DirtyChanged?.Invoke(this, args);
        _eventBus.Publish(new ProjectDirtyChangedEvent { ProjectId = projectId, IsDirty = isDirty });
    }
}
