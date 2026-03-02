
//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.Editor.TblEditor.Services;

/// <summary>
/// Result of a TBL repair analysis.
/// </summary>
public sealed record TblRepairResult(
    string                   RepairedContent,
    IReadOnlyList<DiagnosticEntry> Diagnostics,
    int                      RepairsApplied,
    bool                     WasModified);

/// <summary>
/// Parses raw .tbl content line-by-line, produces <see cref="DiagnosticEntry"/> items
/// with exact line numbers, and builds a repaired version of the content
/// (invalid / duplicate lines removed).
///
/// <para>This service works entirely on strings — no file I/O, no UI dependencies.
/// Safe to call on a background thread.</para>
/// </summary>
public sealed class TblRepairService
{
    private static readonly Regex HexRegex = new("^[0-9A-Fa-f]+$", RegexOptions.Compiled);

    /// <summary>
    /// Analyses <paramref name="rawContent"/> and returns a repair result.
    /// </summary>
    /// <param name="rawContent">Raw text content of a .tbl file.</param>
    /// <param name="fileName">Short display name used in diagnostics (e.g. "table.tbl").</param>
    public TblRepairResult Repair(string rawContent, string? fileName = null)
    {
        var diagnostics    = new List<DiagnosticEntry>();
        var repairedLines  = new List<string>();
        var seenKeys       = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int repairsApplied = 0;

        using var reader = new StringReader(rawContent);
        string? line;
        int lineNumber = 0;

        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;
            var trimmed = line.TrimEnd();

            // ── blank lines and full-line comments are kept unchanged ─────
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#'))
            {
                repairedLines.Add(line);
                continue;
            }

            // ── bookmarks  (StartsWith '(') ───────────────────────────────
            if (trimmed.StartsWith('('))
            {
                repairedLines.Add(line);
                continue;
            }

            // ── special markers: EndLine '*XX' or EndBlock '/XX' ──────────
            if (trimmed.StartsWith('*') || trimmed.StartsWith('/'))
            {
                repairedLines.Add(line);
                continue;
            }

            // ── DTE entry  HEXKEY=VALUE ───────────────────────────────────
            var eqIdx = trimmed.IndexOf('=');
            if (eqIdx > 0)
            {
                var entry = trimmed[..eqIdx].Trim();

                // Strip inline comment from the value part (everything after ' #')
                var valuePart = trimmed[(eqIdx + 1)..];
                var commentIdx = valuePart.IndexOf(" #", StringComparison.Ordinal);
                var value = commentIdx >= 0 ? valuePart[..commentIdx] : valuePart;

                // TBL001 – invalid hex key
                if (!IsValidHex(entry))
                {
                    diagnostics.Add(new DiagnosticEntry(
                        DiagnosticSeverity.Error,
                        "TBL001",
                        $"Invalid hex entry \"{entry}\" — must contain only hex digits with even length (2–16 chars).",
                        FileName:    fileName,
                        FilePath:    null,   // caller fills FilePath
                        Line:        lineNumber));
                    repairsApplied++;
                    // Drop this line from repaired output
                    continue;
                }

                // TBL002 – empty value
                if (string.IsNullOrEmpty(value))
                {
                    diagnostics.Add(new DiagnosticEntry(
                        DiagnosticSeverity.Warning,
                        "TBL002",
                        $"Entry \"{entry}\" has an empty value.",
                        FileName: fileName,
                        FilePath: null,
                        Line:     lineNumber));
                    // Keep the line (not fatal, not removed)
                    repairedLines.Add(line);
                    continue;
                }

                // TBL003 – duplicate key
                if (!seenKeys.Add(entry.ToUpperInvariant()))
                {
                    diagnostics.Add(new DiagnosticEntry(
                        DiagnosticSeverity.Warning,
                        "TBL003",
                        $"Duplicate entry \"{entry}\" — this occurrence will be removed on save.",
                        FileName: fileName,
                        FilePath: null,
                        Line:     lineNumber));
                    repairsApplied++;
                    // Drop duplicate from repaired output
                    continue;
                }

                repairedLines.Add(line);
                continue;
            }

            // ── TBL004 – unrecognised line ────────────────────────────────
            diagnostics.Add(new DiagnosticEntry(
                DiagnosticSeverity.Message,
                "TBL004",
                $"Unrecognised line \"{TruncateForDisplay(trimmed)}\" — will be converted to a comment on save.",
                FileName: fileName,
                FilePath: null,
                Line:     lineNumber));
            // Convert to comment in repaired output
            repairedLines.Add("# " + trimmed);
            repairsApplied++;
        }

        var repairedContent = string.Join(Environment.NewLine, repairedLines);
        bool wasModified = repairsApplied > 0;

        return new TblRepairResult(repairedContent, diagnostics, repairsApplied, wasModified);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static bool IsValidHex(string entry)
        => entry.Length is >= 2 and <= 16
        && entry.Length % 2 == 0
        && HexRegex.IsMatch(entry);

    private static string TruncateForDisplay(string s, int max = 60)
        => s.Length <= max ? s : s[..max] + "…";
}
