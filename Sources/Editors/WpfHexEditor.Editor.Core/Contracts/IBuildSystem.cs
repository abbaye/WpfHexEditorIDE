// ==========================================================
// Project: WpfHexEditor.Editor.Core
// File: Contracts/IBuildSystem.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Central contract for the IDE build system.
//     Implementations: WpfHexEditor.BuildSystem (orchestrator),
//     WpfHexEditor.Plugins.Build.MSBuild (MSBuild adapter).
//
// Architecture Notes:
//     Pattern: Command (BuildSolutionAsync, BuildProjectAsync, …)
//     - All build operations are async to keep the UI responsive.
//     - HasActiveBuild gates toolbar/menu CanExecute bindings.
//     - Events (BuildStarted, BuildSucceeded, etc.) are published via IDEEventBus
//       by the BuildSystem implementation — not raised directly here.
// ==========================================================

namespace WpfHexEditor.Editor.Core;

/// <summary>
/// Orchestrates compilation of solutions and projects.
/// Obtain via <c>IIDEHostContext.BuildSystem</c>.
/// </summary>
public interface IBuildSystem
{
    /// <summary><c>true</c> when a build is currently in progress.</summary>
    bool HasActiveBuild { get; }

    /// <summary>Builds all projects in the active solution.</summary>
    Task<BuildResult> BuildSolutionAsync(CancellationToken ct = default);

    /// <summary>Builds a single project identified by <paramref name="projectId"/>.</summary>
    Task<BuildResult> BuildProjectAsync(string projectId, CancellationToken ct = default);

    /// <summary>Clean then build the entire solution.</summary>
    Task<BuildResult> RebuildSolutionAsync(CancellationToken ct = default);

    /// <summary>Clean then build a single project.</summary>
    Task<BuildResult> RebuildProjectAsync(string projectId, CancellationToken ct = default);

    /// <summary>Removes all build outputs for the active solution.</summary>
    Task CleanSolutionAsync(CancellationToken ct = default);

    /// <summary>Removes all build outputs for a single project.</summary>
    Task CleanProjectAsync(string projectId, CancellationToken ct = default);

    /// <summary>Requests cancellation of the active build. No-op if no build is running.</summary>
    void CancelBuild();

    /// <summary>
    /// Builds only projects whose source files changed since the last successful build,
    /// plus their transitive dependents.
    /// Falls back to a full solution build when all projects are dirty or no tracker is active.
    /// </summary>
    Task<BuildResult> BuildDirtyAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns <see langword="true"/> when the project has file changes since
    /// its last successful build (or when no baseline exists).
    /// Always returns <see langword="true"/> when incremental tracking is not configured.
    /// </summary>
    bool IsProjectDirty(string projectId);
}
