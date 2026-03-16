// ==========================================================
// Project: WpfHexEditor.PluginDev
// File: Build/PluginBuildOrchestrator.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Orchestrates building a plugin project using the MSBuild API.
//     Routes build output to IProgress<string> and collects
//     diagnostics for display in the PluginDevLogPanel.
//
// Architecture Notes:
//     Pattern: Façade — wraps MSBuild complexity behind a single
//     async BuildAsync() call.
//     Uses Microsoft.Build.Locator for runtime MSBuild discovery.
// ==========================================================

using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Locator;

namespace WpfHexEditor.PluginDev.Build;

/// <summary>
/// Builds a plugin .csproj/.whproj using the MSBuild execution API.
/// </summary>
public sealed class PluginBuildOrchestrator
{
    // -----------------------------------------------------------------------
    // MSBuildLocator — one-time initialisation per AppDomain
    // -----------------------------------------------------------------------

    private static readonly object    _initLock    = new();
    private static          bool      _registered;

    private static void EnsureRegistered()
    {
        if (_registered) return;
        lock (_initLock)
        {
            if (_registered) return;
            if (MSBuildLocator.CanRegister) MSBuildLocator.RegisterDefaults();
            _registered = true;
        }
    }

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>
    /// Builds <paramref name="projectFilePath"/> in the specified configuration.
    /// </summary>
    /// <param name="projectFilePath">Absolute path to .csproj or .whproj.</param>
    /// <param name="configuration">Build configuration name (default: <c>"Debug"</c>).</param>
    /// <param name="progress">Optional progress sink for log lines.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Build result with success flag, output assembly path, and diagnostics.</returns>
    public Task<PluginBuildResult> BuildAsync(
        string              projectFilePath,
        string              configuration = "Debug",
        IProgress<string>?  progress      = null,
        CancellationToken   ct            = default)
    {
        EnsureRegistered();

        return Task.Run(() => InvokeBuild(projectFilePath, configuration, progress, ct), ct);
    }

    // -----------------------------------------------------------------------
    // Implementation
    // -----------------------------------------------------------------------

    private static PluginBuildResult InvokeBuild(
        string             projectFilePath,
        string             configuration,
        IProgress<string>? progress,
        CancellationToken  ct)
    {
        var logger   = new PluginBuildLogger(progress);
        var props    = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Configuration"] = configuration,
            ["Platform"]      = "AnyCPU",
        };

        var request = new BuildRequestData(
            projectFilePath,
            props,
            null,
            new[] { "Build" },
            null);

        var parameters = new BuildParameters
        {
            Loggers = [logger],
        };

        var result = BuildManager.DefaultBuildManager.Build(parameters, request);

        ct.ThrowIfCancellationRequested();

        var outputAssembly = string.Empty;
        if (result.ResultsByTarget.TryGetValue("Build", out var buildResult))
        {
            var item = buildResult.Items?.FirstOrDefault();
            outputAssembly = item?.ItemSpec ?? string.Empty;
        }

        return new PluginBuildResult(
            IsSuccess:      result.OverallResult == BuildResultCode.Success,
            OutputAssembly: outputAssembly,
            Errors:         [.. logger.Errors],
            Warnings:       [.. logger.Warnings]);
    }
}

// -----------------------------------------------------------------------
// Build result record
// -----------------------------------------------------------------------

/// <summary>Result of a plugin build operation.</summary>
public sealed record PluginBuildResult(
    bool                         IsSuccess,
    string                       OutputAssembly,
    IReadOnlyList<string>        Errors,
    IReadOnlyList<string>        Warnings);

// -----------------------------------------------------------------------
// MSBuild logger
// -----------------------------------------------------------------------

/// <summary>
/// In-process MSBuild logger that routes messages to an <see cref="IProgress{T}"/> sink.
/// </summary>
internal sealed class PluginBuildLogger : ILogger
{
    private readonly IProgress<string>? _progress;
    private readonly List<string>       _errors   = [];
    private readonly List<string>       _warnings = [];
    private readonly object             _sync     = new();

    public IReadOnlyList<string> Errors   => _errors;
    public IReadOnlyList<string> Warnings => _warnings;

    // ILogger members
    public string?    Parameters { get; set; }
    public LoggerVerbosity Verbosity { get; set; } = LoggerVerbosity.Minimal;

    internal PluginBuildLogger(IProgress<string>? progress) => _progress = progress;

    public void Initialize(IEventSource eventSource)
    {
        eventSource.ErrorRaised   += OnError;
        eventSource.WarningRaised += OnWarning;
        eventSource.MessageRaised += OnMessage;
    }

    public void Shutdown() { }

    // -----------------------------------------------------------------------

    private void OnError(object sender, BuildErrorEventArgs e)
    {
        var msg = $"  error {e.Code}: {e.Message} ({e.File}:{e.LineNumber})";
        lock (_sync) _errors.Add(msg);
        _progress?.Report($"[Error] {msg}");
    }

    private void OnWarning(object sender, BuildWarningEventArgs e)
    {
        var msg = $"  warning {e.Code}: {e.Message} ({e.File}:{e.LineNumber})";
        lock (_sync) _warnings.Add(msg);
        _progress?.Report($"[Warning] {msg}");
    }

    private void OnMessage(object sender, BuildMessageEventArgs e)
    {
        if (e.Importance <= MessageImportance.Normal)
            _progress?.Report(e.Message ?? string.Empty);
    }
}
