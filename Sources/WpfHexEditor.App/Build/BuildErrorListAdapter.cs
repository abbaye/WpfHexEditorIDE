// ==========================================================
// Project: WpfHexEditor.App
// File: Build/BuildErrorListAdapter.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     IDiagnosticSource that bridges MSBuild errors/warnings into the
//     IDE ErrorPanel. Receives BuildResult diagnostics after each build
//     and raises DiagnosticsChanged so ErrorPanel refreshes automatically.
//
// Architecture Notes:
//     Pattern: Adapter + Observer
//     - Implements IDiagnosticSource → consumed by ErrorPanel.AddSource()
//     - Listens for BuildSucceededEvent / BuildFailedEvent to refresh entries
// ==========================================================

using WpfHexEditor.Editor.Core;
using WpfHexEditor.Events;
using WpfHexEditor.Events.IDEEvents;

namespace WpfHexEditor.App.Build;

/// <summary>
/// Populates the IDE ErrorPanel with diagnostics from the last build.
/// </summary>
internal sealed class BuildErrorListAdapter : IDiagnosticSource, IDisposable
{
    private readonly IDisposable[]           _subscriptions;
    private List<DiagnosticEntry>            _entries = [];

    // -----------------------------------------------------------------------

    public BuildErrorListAdapter(IIDEEventBus eventBus)
    {
        if (eventBus is null) throw new ArgumentNullException(nameof(eventBus));

        _subscriptions =
        [
            eventBus.Subscribe<BuildSucceededEvent>(OnBuildSucceeded),
            eventBus.Subscribe<BuildFailedEvent>   (OnBuildFailed),
            eventBus.Subscribe<BuildStartedEvent>  (OnBuildStarted),
        ];
    }

    // -----------------------------------------------------------------------
    // IDiagnosticSource
    // -----------------------------------------------------------------------

    public string SourceLabel => "Build";

    public IReadOnlyList<DiagnosticEntry> GetDiagnostics() => _entries;

    public event EventHandler? DiagnosticsChanged;

    // -----------------------------------------------------------------------
    // Public entry point — called by MainWindow.Build after each build
    // -----------------------------------------------------------------------

    /// <summary>
    /// Replaces current diagnostics with those from the finished build.
    /// </summary>
    public void SetDiagnostics(IEnumerable<BuildDiagnostic> diagnostics)
    {
        _entries = diagnostics
            .Select(d => new DiagnosticEntry(
                Severity    : MapSeverity(d.Severity),
                Code        : d.Code,
                Description : d.Message,
                ProjectName : d.ProjectName,
                FileName    : d.FilePath is null ? null : System.IO.Path.GetFileName(d.FilePath),
                FilePath    : d.FilePath,
                Line        : d.Line,
                Column      : d.Column))
            .ToList();

        DiagnosticsChanged?.Invoke(this, EventArgs.Empty);
    }

    // -----------------------------------------------------------------------
    // Handlers
    // -----------------------------------------------------------------------

    private void OnBuildStarted(BuildStartedEvent _)
    {
        _entries = [];
        DiagnosticsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnBuildSucceeded(BuildSucceededEvent _)
    {
        // Entries will be set externally by MainWindow.Build after the awaited BuildAsync returns.
    }

    private void OnBuildFailed(BuildFailedEvent _)
    {
        // Entries will be set externally by MainWindow.Build after the awaited BuildAsync returns.
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static DiagnosticSeverity MapSeverity(DiagnosticSeverity s) => s;

    public void Dispose()
    {
        foreach (var sub in _subscriptions)
            sub.Dispose();
    }
}
