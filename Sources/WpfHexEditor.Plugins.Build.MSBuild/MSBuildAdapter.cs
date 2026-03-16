// ==========================================================
// Project: WpfHexEditor.Plugins.Build.MSBuild
// File: MSBuildAdapter.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     IBuildAdapter implementation that compiles .csproj / .vbproj
//     projects using Microsoft.Build via MSBuildLocator.
//     A NuGet restore step is executed before every build if the
//     project assets file is missing (first build or after clean).
//
// Architecture Notes:
//     Pattern: Adapter — bridges IBuildAdapter to Microsoft.Build API.
//     MSBuild host must be registered ONCE per AppDomain (RegisterOnce).
//     Build is executed out-of-process safe: Microsoft.Build.Locator
//     discovers the MSBuild installation at runtime.
// ==========================================================

using Microsoft.Build.Execution;
using Microsoft.Build.Locator;
using WpfHexEditor.BuildSystem;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.Plugins.Build.MSBuild;

/// <summary>
/// <see cref="IBuildAdapter"/> that invokes MSBuild to compile VS project files.
/// </summary>
public sealed class MSBuildAdapter : IBuildAdapter
{
    private static readonly object _initLock   = new();
    private static          bool   _registered;

    // -----------------------------------------------------------------------
    // IBuildAdapter
    // -----------------------------------------------------------------------

    public string AdapterId => "msbuild";

    /// <inheritdoc />
    public bool CanBuild(string projectFilePath)
    {
        var ext = System.IO.Path.GetExtension(projectFilePath).ToLowerInvariant();
        return ext is ".csproj" or ".vbproj" or ".fsproj";
    }

    /// <inheritdoc />
    public async Task<BuildResult> BuildAsync(
        string              projectFilePath,
        IBuildConfiguration configuration,
        IProgress<string>?  outputProgress,
        CancellationToken   ct = default)
    {
        EnsureRegistered();

        await NuGetRestoreStep.RestoreIfNeededAsync(projectFilePath, outputProgress, ct);

        return await Task.Run(() => InvokeMSBuild(projectFilePath, configuration, outputProgress, "Build"), ct);
    }

    /// <inheritdoc />
    public async Task CleanAsync(
        string              projectFilePath,
        IBuildConfiguration configuration,
        CancellationToken   ct = default)
    {
        EnsureRegistered();
        await Task.Run(() => InvokeMSBuild(projectFilePath, configuration, null, "Clean"), ct);
    }

    // -----------------------------------------------------------------------
    // MSBuild invocation
    // -----------------------------------------------------------------------

    private static BuildResult InvokeMSBuild(
        string              projectFilePath,
        IBuildConfiguration configuration,
        IProgress<string>?  outputProgress,
        string              target)
    {
        var logger = new MSBuildLogger(outputProgress);

        var globalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Configuration"]  = configuration.Name,
            ["Platform"]       = NormalizePlatform(configuration.Platform),
            ["OutputPath"]     = configuration.OutputPath,
            ["Optimize"]       = configuration.Optimize ? "true" : "false",
            ["DefineConstants"] = configuration.DefineConstants,
        };

        var buildParameters = new BuildParameters
        {
            Loggers = [logger],
        };

        var buildRequest = new BuildRequestData(
            projectFilePath,
            globalProperties,
            toolsVersion: null,
            targetsToBuild: [target],
            hostServices: null);

        var sw     = System.Diagnostics.Stopwatch.StartNew();
        var result = BuildManager.DefaultBuildManager.Build(buildParameters, buildRequest);
        sw.Stop();

        var success = result.OverallResult == BuildResultCode.Success;
        return new BuildResult(success, logger.Errors, logger.Warnings, sw.Elapsed);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static void EnsureRegistered()
    {
        lock (_initLock)
        {
            if (_registered) return;
            MSBuildLocator.RegisterDefaults();
            _registered = true;
        }
    }

    /// <summary>
    /// MSBuild expects "AnyCPU" but the UI may display "Any CPU" (with space).
    /// </summary>
    private static string NormalizePlatform(string platform)
        => platform.Replace(" ", string.Empty);
}
