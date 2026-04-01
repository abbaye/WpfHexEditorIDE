// ==========================================================
// Project: WpfHexEditor.Core.AssemblyAnalysis
// File: Services/DiffTextService.cs
// Author: Derek Tremblay
// Created: 2026-03-16
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     BCL-only unified-diff generator for C# source text.
//     ComputeUnifiedDiff() runs a Myers O(ND)-style LCS diff on lines and
//     outputs a standard unified diff with @@ chunk headers and ±3 context lines.
//
// Architecture Notes:
//     Pattern: Service (stateless, pure function).
//     No WPF / no NuGet — safe from the Core layer.
//     LCS via DP table (O(N×M) space) — adequate for decompiled type skeletons
//     which rarely exceed a few hundred lines each.
// ==========================================================

using System.Text;

namespace WpfHexEditor.Core.AssemblyAnalysis.Services;

/// <summary>
/// Generates a standard unified diff between two C# source texts.
/// </summary>
public static class DiffTextService
{
    private const int ContextLines = 3;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Computes a unified diff string between <paramref name="baseline"/> and
    /// <paramref name="target"/> source texts.  Returns an empty string when
    /// the texts are identical.
    /// </summary>
    public static string ComputeUnifiedDiff(string baseline, string target)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(target);

        var oldLines = SplitLines(baseline);
        var newLines = SplitLines(target);

        var edits = ComputeEdits(oldLines, newLines);

        if (edits.TrueForAll(e => e.Kind == EditKind.Equal))
            return string.Empty;

        return FormatUnified(edits, oldLines, newLines);
    }

    // ── Line splitting ────────────────────────────────────────────────────────

    private static List<string> SplitLines(string text)
    {
        var result = new List<string>();
        var start  = 0;

        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '\n') continue;
            var end     = i > 0 && text[i - 1] == '\r' ? i - 1 : i;
            result.Add(text[start..end]);
            start = i + 1;
        }

        if (start < text.Length)
            result.Add(text[start..]);

        return result;
    }

    // ── Edit list via LCS ─────────────────────────────────────────────────────

    private enum EditKind : byte { Equal, Insert, Delete }

    private sealed class Edit(EditKind kind, int oldIndex, int newIndex)
    {
        public EditKind Kind     { get; } = kind;
        public int      OldIndex { get; } = oldIndex;
        public int      NewIndex { get; } = newIndex;
    }

    private static List<Edit> ComputeEdits(List<string> oldLines, List<string> newLines)
    {
        int m = oldLines.Count;
        int n = newLines.Count;

        // LCS DP table  (m+1) × (n+1)
        var dp = new int[m + 1, n + 1];
        for (var i = m - 1; i >= 0; i--)
        for (var j = n - 1; j >= 0; j--)
        {
            dp[i, j] = string.Equals(oldLines[i], newLines[j], StringComparison.Ordinal)
                ? dp[i + 1, j + 1] + 1
                : Math.Max(dp[i + 1, j], dp[i, j + 1]);
        }

        // Backtrack to build edit list
        var edits = new List<Edit>(m + n);
        int oi = 0, ni = 0;

        while (oi < m || ni < n)
        {
            if (oi < m && ni < n && string.Equals(oldLines[oi], newLines[ni], StringComparison.Ordinal))
            {
                edits.Add(new Edit(EditKind.Equal, oi, ni));
                oi++;
                ni++;
            }
            else if (ni < n && (oi >= m || dp[oi, ni + 1] >= dp[oi + 1, ni]))
            {
                edits.Add(new Edit(EditKind.Insert, oi, ni));
                ni++;
            }
            else
            {
                edits.Add(new Edit(EditKind.Delete, oi, ni));
                oi++;
            }
        }

        return edits;
    }

    // ── Unified diff formatter ─────────────────────────────────────────────────

    private static string FormatUnified(List<Edit> edits, List<string> oldLines, List<string> newLines)
    {
        var sb = new StringBuilder();

        // Group edits into hunks separated by gaps wider than 2*ContextLines
        var hunks = BuildHunks(edits);

        foreach (var hunk in hunks)
        {
            AppendHunkHeader(sb, hunk, edits, oldLines, newLines);

            foreach (var edit in hunk)
            {
                switch (edit.Kind)
                {
                    case EditKind.Equal:
                        sb.Append(' ');
                        sb.AppendLine(oldLines[edit.OldIndex]);
                        break;
                    case EditKind.Delete:
                        sb.Append('-');
                        sb.AppendLine(oldLines[edit.OldIndex]);
                        break;
                    case EditKind.Insert:
                        sb.Append('+');
                        sb.AppendLine(newLines[edit.NewIndex]);
                        break;
                }
            }
        }

        return sb.ToString();
    }

    private static List<List<Edit>> BuildHunks(List<Edit> edits)
    {
        var hunks   = new List<List<Edit>>();
        List<Edit>? current = null;
        int         gapSince = 0;

        for (var i = 0; i < edits.Count; i++)
        {
            var edit    = edits[i];
            bool isDiff = edit.Kind != EditKind.Equal;

            if (isDiff)
            {
                if (current is null)
                {
                    current = new List<Edit>();
                    hunks.Add(current);

                    // Back-fill leading context
                    var contextStart = Math.Max(0, i - ContextLines);
                    for (var c = contextStart; c < i; c++)
                        current.Add(edits[c]);
                }
                else if (gapSince > 0 && gapSince > 2 * ContextLines)
                {
                    // Close previous hunk: append trailing context and start new
                    var trailEnd = Math.Min(edits.Count, i - gapSince + ContextLines);
                    for (var c = i - gapSince; c < trailEnd; c++)
                        current.Add(edits[c]);

                    current = new List<Edit>();
                    hunks.Add(current);

                    // Leading context for new hunk
                    var leadStart = Math.Max(0, i - ContextLines);
                    for (var c = leadStart; c < i; c++)
                        current.Add(edits[c]);
                }

                current.Add(edit);
                gapSince = 0;
            }
            else if (current is not null)
            {
                current.Add(edit);
                gapSince++;
            }
        }

        return hunks;
    }

    private static void AppendHunkHeader(
        StringBuilder  sb,
        List<Edit>     hunk,
        List<Edit>     allEdits,
        List<string>   oldLines,
        List<string>   newLines)
    {
        int oldStart = 1, oldCount = 0, newStart = 1, newCount = 0;
        bool firstSet = false;

        foreach (var e in hunk)
        {
            if (!firstSet)
            {
                oldStart = e.Kind != EditKind.Insert ? e.OldIndex + 1 : (e.OldIndex + 1);
                newStart = e.Kind != EditKind.Delete ? e.NewIndex + 1 : (e.NewIndex + 1);
                firstSet = true;
            }

            if (e.Kind != EditKind.Insert) oldCount++;
            if (e.Kind != EditKind.Delete) newCount++;
        }

        sb.AppendLine($"@@ -{oldStart},{oldCount} +{newStart},{newCount} @@");
    }
}
