// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Services/WhfmtExpressionDiagnosticRule.cs
// Description: IDiagnosticRule implementation that runs the R10 WhfmtExpressionValidator
//              against .whfmt files open in CodeEditor. Converts WhfmtValidationIssue
//              items into DiagnosticEntry squiggles (red=error, yellow=warning).
//              Registered once per CodeEditor instance for language-id "whfmt".
// Architecture: Stateless rule — all state lives in DiagnosticEntry objects
//               returned per call. Throttling is handled by EditorPluginIntegration.
// ==========================================================

using System.IO;
using WpfHexEditor.Core.Definitions.Models.Validation;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.Editor.CodeEditor.Services;

/// <summary>
/// R10 expression diagnostic rule for <c>.whfmt</c> files.
/// Surfaces undeclared variable references, unknown function calls, and
/// prose-expression violations as squiggles inside CodeEditor.
/// </summary>
public sealed class WhfmtExpressionDiagnosticRule : IDiagnosticRule
{
    public string RuleId     => "WHFMT.R10";
    public string LanguageId => "whfmt";

    public IEnumerable<DiagnosticEntry> Evaluate(string documentText, string? filePath = null)
    {
        if (string.IsNullOrWhiteSpace(documentText))
            yield break;

        IReadOnlyList<WhfmtValidationIssue> issues;
        try
        {
            issues = WhfmtExpressionValidator.Validate(documentText);
        }
        catch
        {
            // Validator must never crash the editor — swallow unexpected exceptions.
            yield break;
        }

        foreach (var issue in issues)
        {
            yield return new DiagnosticEntry(
                Severity:    MapSeverity(issue.Severity),
                Code:        issue.RuleId,
                Description: FormatMessage(issue),
                FilePath:    filePath,
                FileName:    filePath is not null ? Path.GetFileName(filePath) : null);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static DiagnosticSeverity MapSeverity(WhfmtIssueSeverity s) => s switch
    {
        WhfmtIssueSeverity.Error   => DiagnosticSeverity.Error,
        WhfmtIssueSeverity.Warning => DiagnosticSeverity.Warning,
        _                          => DiagnosticSeverity.Message
    };

    private static string FormatMessage(WhfmtValidationIssue issue)
    {
        if (issue.Path is not null)
            return $"[{issue.RuleId}] {issue.Path}: {issue.Message}";
        return $"[{issue.RuleId}] {issue.Message}";
    }
}
