
//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.App.Services;

/// <summary>
/// An <see cref="IDiagnosticSource"/> that aggregates background-validation diagnostics
/// keyed by absolute file path.  Updated by <see cref="FileMonitorService"/> whenever
/// a watched file changes on disk.
/// </summary>
public sealed class FileMonitorDiagnosticSource : IDiagnosticSource
{
    private readonly Dictionary<string, IReadOnlyList<DiagnosticEntry>> _byFile =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly object _lock = new();

    // -- IDiagnosticSource -------------------------------------------------

    public string SourceLabel => "Background Validator";

    public event EventHandler? DiagnosticsChanged;

    IReadOnlyList<DiagnosticEntry> IDiagnosticSource.GetDiagnostics()
    {
        lock (_lock)
            return _byFile.Values.SelectMany(x => x).ToList();
    }

    // -- Update API (called by FileMonitorService) -------------------------

    /// <summary>
    /// Replaces the diagnostics for <paramref name="filePath"/>.
    /// Fires <see cref="DiagnosticsChanged"/> on the calling thread.
    /// </summary>
    public void UpdateFile(string filePath, IReadOnlyList<DiagnosticEntry> diagnostics)
    {
        lock (_lock)
            _byFile[filePath] = diagnostics;

        DiagnosticsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Removes all diagnostics for <paramref name="filePath"/> (file deleted or no longer watched).
    /// </summary>
    public void RemoveFile(string filePath)
    {
        bool changed;
        lock (_lock)
            changed = _byFile.Remove(filePath);

        if (changed)
            DiagnosticsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Clears all diagnostics (e.g. solution closed).</summary>
    public void Clear()
    {
        lock (_lock)
            _byFile.Clear();

        DiagnosticsChanged?.Invoke(this, EventArgs.Empty);
    }
}
