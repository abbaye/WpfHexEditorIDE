// ==========================================================
// Project: WpfHexEditor.Editor.Core
// File: Diagnostics/DiagnosticsAggregator.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Centralises diagnostics from all registered IDiagnosticsProvider
//     instances (MSBuild, Roslyn, LSP).  The ErrorList panel binds to
//     this aggregator instead of each individual source.
//
// Architecture Notes:
//     Pattern: Aggregator / Registry
//     - Thread-safe: providers list protected by lock.
//     - Re-publishes DiagnosticsChanged whenever any provider changes.
// ==========================================================

namespace WpfHexEditor.Editor.Core.Diagnostics;

/// <summary>
/// Aggregates diagnostic entries from all registered <see cref="IDiagnosticsProvider"/>
/// instances and exposes a unified, filterable view.
/// </summary>
public sealed class DiagnosticsAggregator
{
    private readonly object _lock = new();
    private readonly List<IDiagnosticsProvider> _providers = [];

    // -----------------------------------------------------------------------
    // Registration
    // -----------------------------------------------------------------------

    /// <summary>Registers a diagnostic provider. No-op if already registered.</summary>
    public void RegisterProvider(IDiagnosticsProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        lock (_lock)
        {
            if (_providers.Any(p => p.SourceId == provider.SourceId)) return;
            _providers.Add(provider);
        }
        provider.DiagnosticsChanged += OnProviderChanged;
    }

    /// <summary>Removes a previously registered provider.</summary>
    public void UnregisterProvider(IDiagnosticsProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        lock (_lock) _providers.Remove(provider);
        provider.DiagnosticsChanged -= OnProviderChanged;
    }

    // -----------------------------------------------------------------------
    // Query
    // -----------------------------------------------------------------------

    /// <summary>Returns all diagnostics from all providers, optionally filtered by file.</summary>
    public IReadOnlyList<DiagnosticEntry> GetAll(string? filePath = null)
    {
        IEnumerable<DiagnosticEntry> entries;

        lock (_lock)
            entries = _providers.SelectMany(p => p.GetDiagnostics()).ToList();

        if (!string.IsNullOrWhiteSpace(filePath))
            entries = entries.Where(e =>
                string.Equals(e.FilePath, filePath, StringComparison.OrdinalIgnoreCase));

        return entries.ToList();
    }

    /// <summary>Returns all errors (severity == Error).</summary>
    public IReadOnlyList<DiagnosticEntry> GetErrors()
        => GetAll().Where(e => e.Severity == DiagnosticSeverity.Error).ToList();

    /// <summary>Returns all warnings.</summary>
    public IReadOnlyList<DiagnosticEntry> GetWarnings()
        => GetAll().Where(e => e.Severity == DiagnosticSeverity.Warning).ToList();

    // -----------------------------------------------------------------------
    // Mutation helpers
    // -----------------------------------------------------------------------

    /// <summary>Removes all diagnostics for a document across all providers.</summary>
    public void ClearForDocument(string filePath)
    {
        lock (_lock)
            foreach (var p in _providers)
                p.ClearForDocument(filePath);
    }

    /// <summary>Removes all diagnostics from the specified source.</summary>
    public void ClearSource(string sourceId)
    {
        IDiagnosticsProvider? provider;
        lock (_lock)
            provider = _providers.FirstOrDefault(p => p.SourceId == sourceId);
        provider?.ClearAll();
    }

    // -----------------------------------------------------------------------
    // Events
    // -----------------------------------------------------------------------

    /// <summary>Raised whenever any provider reports a change in diagnostics.</summary>
    public event EventHandler? DiagnosticsChanged;

    private void OnProviderChanged(object? sender, EventArgs e)
        => DiagnosticsChanged?.Invoke(this, EventArgs.Empty);
}
