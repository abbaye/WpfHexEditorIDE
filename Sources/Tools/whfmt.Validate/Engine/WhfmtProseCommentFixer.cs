// ==========================================================
// Project: whfmt.Validate
// File: Engine/WhfmtProseCommentFixer.cs
// Description: Mechanical fixer for prose-expression fields identified by the
//              sweep command. Replaces the expression value with an empty string
//              and inserts a sibling "_comment" field containing the original
//              prose text. This neutralizes the R10 violation while preserving
//              intent for human review.
// Architecture: Pure text transformation (no full JSON re-serialization) to
//               preserve formatting, key order, and JSONC comments.
// ==========================================================

using System.Text.RegularExpressions;
using WpfHexEditor.Core.Definitions.Models.Validation;

namespace WhfmtValidate.Engine;

/// <summary>
/// Applies mechanical prose-expression fixes to .whfmt source text.
/// Returns the patched content, or <c>null</c> if no changes could be made safely.
/// </summary>
internal static partial class WhfmtProseCommentFixer
{
    /// <summary>
    /// For each prose issue whose <see cref="WhfmtValidationIssue.Path"/> points to
    /// an <c>expression</c> field, replaces the value with <c>""</c> and appends a
    /// <c>"_expressionNote"</c> sibling with the original prose text.
    /// Skips any issue where the target cannot be found unambiguously in the text.
    /// </summary>
    internal static string? TryApply(string source, IEnumerable<WhfmtValidationIssue> proseIssues)
    {
        string working = source;
        bool anyChange = false;

        foreach (var issue in proseIssues)
        {
            if (string.IsNullOrEmpty(issue.Source)) continue;

            string? patched = TryPatchOneExpression(working, issue.Source);
            if (patched is not null && patched != working)
            {
                working = patched;
                anyChange = true;
            }
        }

        return anyChange ? working : null;
    }

    // ── Single-expression patch ──────────────────────────────────────────────

    private static string? TryPatchOneExpression(string source, string proseExpression)
    {
        // Build pattern: find  "expression": "<proseExpression>"
        // We require an exact match of the prose text to avoid ambiguity.
        string escaped = Regex.Escape(proseExpression);
        var pattern    = new Regex(
            $"(\"expression\"\\s*:\\s*)\"({escaped})\"",
            RegexOptions.None, TimeSpan.FromSeconds(2));

        var matches = pattern.Matches(source);
        if (matches.Count != 1) return null;   // ambiguous or not found — skip

        var match = matches[0];

        // Replacement: blank the expression, add _expressionNote on the next line
        // Preserve leading whitespace from the matched line.
        int lineStart = source.LastIndexOf('\n', match.Index) + 1;
        string indent = GetIndent(source, lineStart);

        string replacement =
            $"{match.Groups[1].Value}\"\"," +
            $"\n{indent}\"_expressionNote\": \"{EscapeJson(proseExpression)}\"";

        return source[..match.Index] + replacement + source[(match.Index + match.Length)..];
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string GetIndent(string source, int lineStart)
    {
        int i = lineStart;
        while (i < source.Length && source[i] is ' ' or '\t') i++;
        return source[lineStart..i];
    }

    private static string EscapeJson(string s) =>
        s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");
}
