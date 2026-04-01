// ==========================================================
// Project: WpfHexEditor.Editor.Core
// File: Diagnostics/IDiagnosticsProvider.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Contract for a service that supplies DiagnosticEntry items to the
//     DiagnosticsAggregator. Multiple providers can register independently
//     (MSBuild, Roslyn, LSP lint, custom rules).
//
// Architecture Notes:
//     Pattern: Strategy — each provider adds its own diagnostic source.
//     DiagnosticsAggregator collects from all registered providers.
// ==========================================================

namespace WpfHexEditor.Editor.Core.Diagnostics;

/// <summary>
/// Supplies diagnostic entries from one source (MSBuild, Roslyn, LSP, etc.)
/// to the <see cref="DiagnosticsAggregator"/>.
/// </summary>
public interface IDiagnosticsProvider
{
    /// <summary>Unique source identifier (e.g. "msbuild", "lsp", "roslyn").</summary>
    string SourceId { get; }

    /// <summary>
    /// Returns all current diagnostics from this provider.
    /// Called by the aggregator after each build/lint cycle.
    /// </summary>
    IReadOnlyList<DiagnosticEntry> GetDiagnostics();

    /// <summary>Removes all diagnostics for the given document path.</summary>
    void ClearForDocument(string filePath);

    /// <summary>Removes all diagnostics from this provider.</summary>
    void ClearAll();

    /// <summary>Raised when the provider's diagnostic set has changed.</summary>
    event EventHandler DiagnosticsChanged;
}
