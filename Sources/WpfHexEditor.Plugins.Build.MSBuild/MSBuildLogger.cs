// ==========================================================
// Project: WpfHexEditor.Plugins.Build.MSBuild
// File: MSBuildLogger.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     MSBuild ILogger implementation that captures build messages,
//     warnings, and errors and routes them to:
//       - IProgress<string> for real-time OutputPanel output
//       - DiagnosticCollector list for post-build ErrorList population
//
// Architecture Notes:
//     Pattern: Adapter — bridges MSBuild's ILogger to WpfHexEditor types.
//     Thread-safety: MSBuild may invoke logger methods on background threads;
//     all list mutations use lock(_sync).
// ==========================================================

using Microsoft.Build.Framework;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.Plugins.Build.MSBuild;

/// <summary>
/// MSBuild <see cref="ILogger"/> that routes output to an <see cref="IProgress{T}"/>
/// and collects diagnostics into a mutable list.
/// </summary>
internal sealed class MSBuildLogger : ILogger
{
    private readonly IProgress<string>?         _outputProgress;
    private readonly List<BuildDiagnostic>       _errors   = [];
    private readonly List<BuildDiagnostic>       _warnings = [];
    private readonly object                      _sync     = new();

    // -----------------------------------------------------------------------

    public MSBuildLogger(IProgress<string>? outputProgress)
    {
        _outputProgress = outputProgress;
    }

    // -----------------------------------------------------------------------
    // Collected diagnostics (read after build completes)
    // -----------------------------------------------------------------------

    public IReadOnlyList<BuildDiagnostic> Errors   => _errors;
    public IReadOnlyList<BuildDiagnostic> Warnings => _warnings;

    // -----------------------------------------------------------------------
    // ILogger
    // -----------------------------------------------------------------------

    public LoggerVerbosity Verbosity { get; set; } = LoggerVerbosity.Minimal;
    public string?         Parameters { get; set; }

    public void Initialize(IEventSource eventSource)
    {
        eventSource.ErrorRaised   += OnError;
        eventSource.WarningRaised += OnWarning;
        eventSource.MessageRaised += OnMessage;
    }

    public void Shutdown() { /* nothing to dispose */ }

    // -----------------------------------------------------------------------
    // Event handlers
    // -----------------------------------------------------------------------

    private void OnError(object sender, BuildErrorEventArgs e)
    {
        var diag = new BuildDiagnostic(
            e.File, e.LineNumber, e.ColumnNumber,
            e.Code ?? "MSB0000", e.Message ?? string.Empty,
            DiagnosticSeverity.Error,
            ProjectId: null, ProjectName: e.ProjectFile);

        lock (_sync) _errors.Add(diag);

        _outputProgress?.Report($"  error {diag.Code}: {diag.Message}  [{diag.FilePath}({diag.Line},{diag.Column})]");
    }

    private void OnWarning(object sender, BuildWarningEventArgs e)
    {
        var diag = new BuildDiagnostic(
            e.File, e.LineNumber, e.ColumnNumber,
            e.Code ?? "MSB0000", e.Message ?? string.Empty,
            DiagnosticSeverity.Warning,
            ProjectId: null, ProjectName: e.ProjectFile);

        lock (_sync) _warnings.Add(diag);

        _outputProgress?.Report($"  warning {diag.Code}: {diag.Message}  [{diag.FilePath}({diag.Line},{diag.Column})]");
    }

    private void OnMessage(object sender, BuildMessageEventArgs e)
    {
        if (Verbosity == LoggerVerbosity.Quiet) return;
        if (Verbosity == LoggerVerbosity.Minimal && e.Importance != MessageImportance.High) return;

        _outputProgress?.Report(e.Message ?? string.Empty);
    }
}
