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
//     - ClearDiagnostics() / SetDiagnostics() are called explicitly by MainWindow.Build
//       on the UI thread to eliminate async event-bus race conditions.
// ==========================================================

using WpfHexEditor.Editor.Core;
using WpfHexEditor.Events;

namespace WpfHexEditor.App.Build;

/// <summary>
/// Populates the IDE ErrorPanel with diagnostics from the last build.
/// </summary>
internal sealed class BuildErrorListAdapter : IDiagnosticSource, IDisposable
{
    private List<DiagnosticEntry> _entries = [];

    // -----------------------------------------------------------------------

    public BuildErrorListAdapter(IIDEEventBus eventBus)
    {
        // Event bus parameter kept for API compatibility; no subscriptions needed.
        // Clearing and populating diagnostics is driven explicitly by MainWindow.Build
        // via ClearDiagnostics() / SetDiagnostics() to avoid async event-bus race conditions.
        _ = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
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
    // Public entry points — called explicitly by MainWindow.Build on the UI thread
    // -----------------------------------------------------------------------

    /// <summary>
    /// Clears all diagnostics. Call this on the UI thread before starting a build
    /// to ensure the error list is empty before new results arrive (race-free).
    /// </summary>
    public void ClearDiagnostics()
    {
        _entries = [];
        DiagnosticsChanged?.Invoke(this, EventArgs.Empty);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static DiagnosticSeverity MapSeverity(DiagnosticSeverity s) => s;

    public void Dispose() { }
}
