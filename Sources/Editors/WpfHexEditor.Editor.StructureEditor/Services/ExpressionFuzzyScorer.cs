//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////
// Project: WpfHexEditor.Editor.StructureEditor
// File: Services/ExpressionFuzzyScorer.cs
// Description: VS-level fuzzy scoring for ExpressionCompletePopup suggestion filtering.
//              Self-contained copy of SmartCompleteFuzzyScorer (CodeEditor assembly)
//              renamed to avoid cross-assembly internal dependency.
// Architecture Notes:
//     Stateless — all methods are static. Operates on immutable inputs.
//     Called per-filter pass inside ExpressionCompletionProvider.GetSuggestions().
//////////////////////////////////////////////

namespace WpfHexEditor.Editor.StructureEditor.Services;

/// <summary>
/// Scores a completion candidate against a query string.
/// Higher scores indicate better matches.
/// </summary>
internal static class ExpressionFuzzyScorer
{
    private const int ScoreExactPrefix      = 1000;
    private const int ScoreCamelAcronym     = 800;
    private const int ScoreConsecutiveBase  = 500;
    private const int ScoreConsecutiveBonus = 10;
    private const int ScoreSubsequence      = 200;
    private const int ScoreNoMatch          = -1;

    /// <summary>
    /// Scores <paramref name="candidate"/> against <paramref name="query"/>.
    /// Returns <see cref="ScoreNoMatch"/> (-1) when no match is found.
    /// <paramref name="matchedIndices"/> is populated with 0-based char positions
    /// inside <paramref name="candidate"/> for bold-highlight rendering.
    /// </summary>
    internal static int Score(string query, string candidate, out List<int> matchedIndices)
    {
        matchedIndices = [];

        if (string.IsNullOrEmpty(query))
            return ScoreSubsequence;

        if (string.IsNullOrEmpty(candidate))
            return ScoreNoMatch;

        if (candidate.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            for (int i = 0; i < query.Length; i++)
                matchedIndices.Add(i);
            return ScoreExactPrefix;
        }

        if (TryCamelAcronym(query, candidate, matchedIndices))
            return ScoreCamelAcronym;

        matchedIndices.Clear();

        int consecutiveScore = TryConsecutive(query, candidate, matchedIndices);
        if (consecutiveScore >= 0)
            return consecutiveScore;

        matchedIndices.Clear();

        if (TrySubsequence(query, candidate, matchedIndices))
            return ScoreSubsequence;

        return ScoreNoMatch;
    }

    private static bool TryCamelAcronym(string query, string candidate, List<int> indices)
    {
        var boundaries = new List<int>(16) { 0 };
        for (int i = 1; i < candidate.Length; i++)
        {
            if (char.IsUpper(candidate[i]) || candidate[i - 1] == '_' || candidate[i - 1] == '.')
                boundaries.Add(i);
        }

        if (boundaries.Count < query.Length)
            return false;

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
            if (!found) { indices.Clear(); return false; }
        }
        return true;
    }

    private static int TryConsecutive(string query, string candidate, List<int> indices)
    {
        int bestScore = ScoreNoMatch;
        List<int> bestIndices = [];

        for (int start = 0; start <= candidate.Length - query.Length; start++)
        {
            if (char.ToUpperInvariant(candidate[start]) != char.ToUpperInvariant(query[0]))
                continue;

            var local = new List<int> { start };
            int ci = start + 1, qi = 1, runLen = 1;

            while (qi < query.Length && ci < candidate.Length)
            {
                if (char.ToUpperInvariant(candidate[ci]) == char.ToUpperInvariant(query[qi]))
                {
                    local.Add(ci); ci++; qi++; runLen++;
                }
                else break;
            }

            if (qi == query.Length)
            {
                int score = ScoreConsecutiveBase + runLen * ScoreConsecutiveBonus;
                if (score > bestScore) { bestScore = score; bestIndices = local; }
            }
        }

        if (bestScore >= 0)
            indices.AddRange(bestIndices);
        return bestScore;
    }

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
                    indices.Add(ci); ci++; found = true; break;
                }
                ci++;
            }
            if (!found) return false;
        }
        return true;
    }
}
