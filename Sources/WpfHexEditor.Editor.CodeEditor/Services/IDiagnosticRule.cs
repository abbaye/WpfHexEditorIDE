// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Services/IDiagnosticRule.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Contract for a single code-quality diagnostic rule applied to
//     document text. Plugins implement this interface and register it
//     via EditorPluginIntegration to extend the editor's validation pipeline.
//
// Architecture Notes:
//     Strategy Pattern — each rule is independent and stateless.
//     Uses WpfHexEditor.Editor.Core.DiagnosticEntry (shared IDE-wide type)
//     so results flow directly into the error panel without conversion.
// ==========================================================

using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.Editor.CodeEditor.Services;

/// <summary>
/// A single pluggable validation rule evaluated against document text.
/// Register instances via <see cref="EditorPluginIntegration"/>.
/// </summary>
public interface IDiagnosticRule
{
    /// <summary>Unique identifier for this rule (e.g. <c>"MY_PLUGIN.RULE_001"</c>).</summary>
    string RuleId { get; }

    /// <summary>
    /// Language identifier this rule applies to (e.g. <c>"json"</c>).
    /// Use <c>"*"</c> to apply to all languages.
    /// </summary>
    string LanguageId { get; }

    /// <summary>
    /// Evaluates the rule against the full document text.
    /// Must be fast and non-blocking — called on the UI thread after every edit.
    /// </summary>
    /// <param name="documentText">Current flat document text.</param>
    /// <param name="filePath">Absolute path of the document (for DiagnosticEntry.FilePath).</param>
    /// <returns>Zero or more diagnostics. Return empty enumerable if no issues.</returns>
    IEnumerable<DiagnosticEntry> Evaluate(string documentText, string? filePath = null);
}
