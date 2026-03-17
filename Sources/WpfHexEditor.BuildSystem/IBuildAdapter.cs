// ==========================================================
// Project: WpfHexEditor.BuildSystem
// File: IBuildAdapter.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Pluggable build backend contract.
//     WpfHexEditor.Plugins.Build.MSBuild implements this to delegate
//     actual compilation to MSBuild via Microsoft.Build.Locator.
//
// Architecture Notes:
//     Pattern: Adapter — BuildSystem owns the orchestration logic;
//     IBuildAdapter owns the tool-specific invocation.
// ==========================================================

using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.BuildSystem;

/// <summary>
/// Contract for a build backend (e.g. MSBuild, custom compiler).
/// </summary>
public interface IBuildAdapter
{
    /// <summary>Unique identifier for this adapter (e.g. <c>"msbuild"</c>).</summary>
    string AdapterId { get; }

    /// <summary>
    /// Returns <c>true</c> if this adapter can build the project at <paramref name="projectFilePath"/>.
    /// </summary>
    bool CanBuild(string projectFilePath);

    /// <summary>Builds the project at <paramref name="projectFilePath"/>.</summary>
    Task<BuildResult> BuildAsync(
        string               projectFilePath,
        IBuildConfiguration  configuration,
        IProgress<string>?   outputProgress,
        CancellationToken    ct = default);

    /// <summary>Cleans build outputs for <paramref name="projectFilePath"/>.</summary>
    Task CleanAsync(
        string              projectFilePath,
        IBuildConfiguration configuration,
        IProgress<string>?  outputProgress,
        CancellationToken   ct = default);
}
