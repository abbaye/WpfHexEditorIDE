// ==========================================================
// Project: WpfHexEditor.Core.LSP
// File: Diagnostics/DiagnosticsEngine.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Applies registered IDiagnosticRule instances to ParseResult data.
//     Integrates with the IDE-wide DiagnosticsAggregator via events.
//     Runs rules on a background thread to avoid blocking the UI.
//
// Architecture Notes:
//     Pattern: Pipeline + Observer
//     - Rules are registered via RegisterRule() / UnregisterRule().
//     - Process() is called by Integration layer after each IncrementalParser update.
//     - DiagnosticsUpdated event fires on the ThreadPool after completion.
//     - Uses WpfHexEditor.Editor.Core.DiagnosticEntry (shared IDE type).
// ==========================================================

using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.CodeEditor.Services;
using WpfHexEditor.Core.LSP.Parsing;

namespace WpfHexEditor.Core.LSP.Diagnostics;

/// <summary>
/// Applies <see cref="IDiagnosticRule"/> instances to parsed document results
/// and raises <see cref="DiagnosticsUpdated"/> when the analysis is complete.
/// </summary>
public sealed class DiagnosticsEngine
{
    private readonly object _lock = new();
    private readonly List<IDiagnosticRule> _rules = [];

    // Latest document text cached to allow re-running after rule changes.
    private string? _lastText;
    private string? _lastFilePath;

    // -----------------------------------------------------------------------
    // Registration
    // -----------------------------------------------------------------------

    /// <summary>Adds a diagnostic rule to the pipeline.</summary>
    public void RegisterRule(IDiagnosticRule rule)
    {
        lock (_lock) _rules.Add(rule);
    }

    /// <summary>Removes a diagnostic rule from the pipeline.</summary>
    public void UnregisterRule(IDiagnosticRule rule)
    {
        lock (_lock) _rules.Remove(rule);
    }

    // -----------------------------------------------------------------------
    // Processing
    // -----------------------------------------------------------------------

    /// <summary>
    /// Schedules a background diagnostic pass for the given document text.
    /// <see cref="DiagnosticsUpdated"/> fires when all rules complete.
    /// </summary>
    public void Process(string documentText, string filePath, string languageId)
    {
        _lastText     = documentText;
        _lastFilePath = filePath;

        List<IDiagnosticRule> snapshot;
        lock (_lock)
            snapshot = [.. _rules.Where(r => r.LanguageId == "*" || r.LanguageId.Equals(languageId, StringComparison.OrdinalIgnoreCase))];

        if (snapshot.Count == 0) return;

        Task.Run(() => RunRules(snapshot, documentText, filePath));
    }

    // -----------------------------------------------------------------------
    // Events
    // -----------------------------------------------------------------------

    /// <summary>
    /// Raised on a background thread after all rules have been evaluated.
    /// </summary>
    public event EventHandler<DiagnosticsResult>? DiagnosticsUpdated;

    // -----------------------------------------------------------------------
    // Private
    // -----------------------------------------------------------------------

    private void RunRules(
        IReadOnlyList<IDiagnosticRule> rules,
        string text,
        string filePath)
    {
        var entries = new List<DiagnosticEntry>();
        foreach (var rule in rules)
        {
            try
            {
                entries.AddRange(rule.Evaluate(text, filePath));
            }
            catch
            {
                // Defensive: a faulty plugin rule must not crash the IDE.
            }
        }

        DiagnosticsUpdated?.Invoke(this, new DiagnosticsResult(filePath, entries));
    }
}

/// <summary>Result payload raised by <see cref="DiagnosticsEngine.DiagnosticsUpdated"/>.</summary>
/// <param name="FilePath">Document that was analysed.</param>
/// <param name="Entries">All diagnostics produced by this pass.</param>
public sealed record DiagnosticsResult(
    string                   FilePath,
    IReadOnlyList<DiagnosticEntry> Entries);
