// ==========================================================
// Project: WpfHexEditor.BuildSystem
// File: IIncrementalBuildTracker.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Description:
//     Contract for tracking per-project dirty state since the last
//     successful build. Backed by FileSystemWatcher via BuildFileWatcher.
//
// Architecture Notes:
//     Pattern: Observer + State
//     - DirtyChanged event is published on whatever thread the FSW fires on.
//       Consumers (Solution Explorer VM) must marshal to the UI thread.
//     - Projects with no baseline snapshot are considered dirty by default
//       (first launch after IDE start — no prior build recorded).
// ==========================================================

namespace WpfHexEditor.Core.BuildSystem;

/// <summary>
/// Tracks which projects have been modified (source files changed) since
/// their last successful build.
/// </summary>
public interface IIncrementalBuildTracker
{
    /// <summary>
    /// Returns <see langword="true"/> when the project has no build snapshot
    /// or at least one tracked file changed since <see cref="RecordSuccess"/> was last called.
    /// </summary>
    bool IsProjectDirty(string projectId);

    /// <summary>
    /// Returns the subset of <paramref name="projectIds"/> whose dirty flag is set.
    /// </summary>
    IReadOnlyList<string> GetDirtyProjects(IEnumerable<string> projectIds);

    /// <summary>
    /// Clears the dirty flag for the given project and snapshots the current time.
    /// Call this after a project builds successfully.
    /// </summary>
    void RecordSuccess(string projectId);

    /// <summary>
    /// Forces the dirty flag on for a project (e.g. after a manual file edit
    /// that the FSW may not have caught yet).
    /// </summary>
    void Invalidate(string projectId);

    /// <summary>Fired when a project's dirty state changes.</summary>
    event EventHandler<ProjectDirtyChangedEventArgs> DirtyChanged;
}

/// <summary>Event arguments for <see cref="IIncrementalBuildTracker.DirtyChanged"/>.</summary>
public sealed class ProjectDirtyChangedEventArgs : EventArgs
{
    /// <summary>The project whose dirty state changed.</summary>
    public string ProjectId { get; init; } = string.Empty;

    /// <summary><see langword="true"/> if the project is now dirty; <see langword="false"/> if it became clean.</summary>
    public bool IsDirty { get; init; }
}
