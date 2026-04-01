// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Helpers/SmartCompleteFuzzyScorer.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-31
// Description:
//     VS-level fuzzy scoring for SmartComplete suggestion filtering and ranking.
//     Implements prefix, CamelCase-acronym, consecutive, and subsequence matching
//     with scores that naturally sort more-relevant items to the top.
//
// Architecture Notes:
//     Stateless — all methods are static or operate on immutable inputs.
//     Called per-filter pass inside SmartCompletePopup.FilterSuggestions().
//     Matched character indices are returned for optional bold-highlight rendering.
// ==========================================================

using System;
using System.Collections.Generic;

namespace WpfHexEditor.Editor.CodeEditor.Helpers;

/// <summary>
/// Scores a completion candidate against a query string.
/// Higher scores indicate better matches.
/// </summary>
internal static class SmartCompleteFuzzyScorer
{
    // ── Score constants ───────────────────────────────────────────────────────────

    private const int ScoreExactPrefix      = 1000;
    private const int ScoreCamelAcronym     = 800;
    private const int ScoreConsecutiveBase  = 500;
    private const int ScoreConsecutiveBonus = 10;   // per consecutive char
    private const int ScoreSubsequence      = 200;
    private const int ScoreNoMatch          = -1;

    // ── Public API ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Scores <paramref name="candidate"/> against <paramref name="query"/>.
    /// Returns <see cref="ScoreNoMatch"/> (-1) when no match is found.
    /// <paramref name="matchedIndices"/> is populated with 0-based char positions
    /// inside <paramref name="candidate"/> that were matched (for bold-highlight use).
    /// </summary>
    internal static int Score(
        string query,
        string candidate,
        out List<int> matchedIndices)
    {
        matchedIndices = new List<int>();

        if (string.IsNullOrEmpty(query))
        {
            // Empty query — everything matches; no indices to highlight.
            return ScoreSubsequence;
        }

        if (string.IsNullOrEmpty(candidate))
            return ScoreNoMatch;

        // 1. Exact prefix match (highest priority)
        if (candidate.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            for (int i = 0; i < query.Length; i++)
                matchedIndices.Add(i);
            return ScoreExactPrefix;
        }

        // 2. CamelCase acronym match  (query="lc" → "LoadConfig", "lC" → "LoadCommand")
        if (TryCamelAcronym(query, candidate, matchedIndices))
            return ScoreCamelAcronym;

        matchedIndices.Clear();

        // 3. Consecutive chars match  (query="cfg" matches "myConfig" starting at index 2)
        int consecutiveScore = TryConsecutive(query, candidate, matchedIndices);
        if (consecutiveScore >= 0)
            return consecutiveScore;

        matchedIndices.Clear();

        // 4. Subsequence match  (all query chars present in order, possibly non-adjacent)
        if (TrySubsequence(query, candidate, matchedIndices))
            return ScoreSubsequence;

        return ScoreNoMatch;
    }

    // ── Private helpers ───────────────────────────────────────────────────────────

    /// <summary>
    /// Checks whether every char of <paramref name="query"/> matches an uppercase (or first-char)
    /// letter inside <paramref name="candidate"/>.  "lc" → "LoadConfig" (L, C).
    /// </summary>
    private static bool TryCamelAcronym(string query, string candidate, List<int> indices)
    {
        // Collect positions of word-boundary chars (upper-case or after '_' or position 0)
        var boundaries = new List<int>(16);
        boundaries.Add(0);  // first char always counts
        for (int i = 1; i < candidate.Length; i++)
        {
            if (char.IsUpper(candidate[i]) || candidate[i - 1] == '_' || candidate[i - 1] == '.')
                boundaries.Add(i);
        }

        if (boundaries.Count < query.Length)
            return false;

        // Try to match query[j] against boundaries[j] (greedy left-to-right)
        int bi = 0;
        for (int qi = 0; qi < query.Length; qi++)
        {
            bool found = false;
            while (bi < boundaries.Count)
            {
                int pos = boundaries[bi++];
                if (char.ToUpperInvariant(candidate[pos]) == char.ToUpperInvariant(query[qi]))
                {
                    indices.Add(pos);
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                indices.Clear();
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Finds the longest consecutive match of <paramref name="query"/> inside
    /// <paramref name="candidate"/>.  Returns the score (≥ScoreConsecutiveBase) or -1.
    /// </summary>
    private static int TryConsecutive(string query, string candidate, List<int> indices)
    {
        int bestScore = ScoreNoMatch;
        List<int> bestIndices = new();

        for (int start = 0; start <= candidate.Length - query.Length; start++)
        {
            if (char.ToUpperInvariant(candidate[start]) != char.ToUpperInvariant(query[0]))
                continue;

            var local  = new List<int> { start };
            int ci     = start + 1;
            int qi     = 1;
            int runLen = 1;

            while (qi < query.Length && ci < candidate.Length)
            {
                if (char.ToUpperInvariant(candidate[ci]) == char.ToUpperInvariant(query[qi]))
                {
                    local.Add(ci);
                    ci++;
                    qi++;
                    runLen++;
                }
                else break;
            }

            if (qi == query.Length)
            {
                // Full consecutive match starting at `start`
                int score = ScoreConsecutiveBase + runLen * ScoreConsecutiveBonus;
                if (score > bestScore)
                {
                    bestScore   = score;
                    bestIndices = local;
                }
            }
        }

        if (bestScore >= 0)
            indices.AddRange(bestIndices);
        return bestScore;
    }

    /// <summary>
    /// Returns true when all chars of <paramref name="query"/> appear in order
    /// (non-contiguously) inside <paramref name="candidate"/>.
    /// </summary>
    private static bool TrySubsequence(string query, string candidate, List<int> indices)
    {
        int ci = 0;
        for (int qi = 0; qi < query.Length; qi++)
        {
            bool found = false;
            while (ci < candidate.Length)
            {
                if (char.ToUpperInvariant(candidate[ci]) == char.ToUpperInvariant(query[qi]))
                {
                    indices.Add(ci);
                    ci++;
                    found = true;
                    break;
                }
                ci++;
            }
            if (!found) return false;
        }
        return true;
    }
}
