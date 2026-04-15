//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////
// Project: WpfHexEditor.Editor.StructureEditor
// File: Services/ExpressionCompletionProvider.cs
// Description: Pure domain service — given an ExpressionCompleteContext, returns
//              an ordered list of ExpressionCompleteSuggestion items.
//              No WPF dependency, no side effects, no I/O.
// Architecture Notes:
//     Called synchronously on UI thread; all data is in-memory.
//     Uses ExpressionFuzzyScorer for ranking.
//////////////////////////////////////////////

using WpfHexEditor.Editor.StructureEditor.Models;

namespace WpfHexEditor.Editor.StructureEditor.Services;

internal sealed class ExpressionCompletionProvider
{
    // ── Prefix suggestions (shown when no active prefix) ─────────────────────

    private static readonly IReadOnlyList<ExpressionCompleteSuggestion> PrefixSuggestions =
    [
        new()
        {
            DisplayText   = "var:",
            InsertText    = "var:",
            Icon          = "\uE74B",    // variable glyph
            TypeHint      = "prefix",
            Documentation = "var:variableName — references a previously stored variable.\nExample: var:width",
            SortPriority  = 0,
        },
        new()
        {
            DisplayText   = "calc:",
            InsertText    = "calc:",
            Icon          = "\uE8F4",    // code/formula glyph
            TypeHint      = "prefix",
            Documentation = "calc:expression — arithmetic expression using variables and built-in functions.\nExample: calc:width * height",
            SortPriority  = 1,
        },
        new()
        {
            DisplayText   = "offset:",
            InsertText    = "offset:",
            Icon          = "\uE81E",    // link/anchor glyph
            TypeHint      = "prefix",
            Documentation = "offset:N — fixed byte offset (N = integer or hex 0xNN).\nExample: offset:4",
            SortPriority  = 2,
        },
    ];

    // ── Offset hint (shown after offset: prefix — no real list) ──────────────

    private static readonly IReadOnlyList<ExpressionCompleteSuggestion> OffsetHint =
    [
        new()
        {
            DisplayText   = "<integer or 0xNN>",
            InsertText    = "",
            Icon          = "\uE81E",
            TypeHint      = "hint",
            Documentation = "Enter a decimal integer or hex value (e.g. 4, 0x10).",
            SortPriority  = 0,
        },
    ];

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns suggestions ordered by relevance (descending MatchScore, then SortPriority).
    /// Returns an empty list when nothing meaningful can be suggested.
    /// </summary>
    internal IReadOnlyList<ExpressionCompleteSuggestion> GetSuggestions(ExpressionCompleteContext ctx)
    {
        return ctx.ActivePrefix switch
        {
            null      => FilterAndSort(PrefixSuggestions, ctx.Token),
            "var:"    => BuildVarSuggestions(ctx),
            "calc:"   => BuildCalcSuggestions(ctx),
            "offset:" => OffsetHint,
            _         => [],
        };
    }

    // ── Builders ──────────────────────────────────────────────────────────────

    private static IReadOnlyList<ExpressionCompleteSuggestion> BuildVarSuggestions(
        ExpressionCompleteContext ctx)
    {
        var userVars = ctx.VariableSource.GetVariableNames()
            .Select(name => new ExpressionCompleteSuggestion
            {
                DisplayText   = name,
                InsertText    = name,
                Icon          = "\uE74B",
                TypeHint      = "variable",
                Documentation = $"User-defined variable: {name}",
                SortPriority  = 0,
            });

        var builtInVars = BuiltInFunctionCatalog.BuiltInOutputVars
            .Select(name => new ExpressionCompleteSuggestion
            {
                DisplayText   = name,
                InsertText    = name,
                Icon          = "\uE946",   // auto/built-in glyph
                TypeHint      = "built-in",
                Documentation = $"Built-in output variable set by a function call: {name}",
                SortPriority  = 5,
            });

        return FilterAndSort([.. userVars, .. builtInVars], ctx.Token);
    }

    private static IReadOnlyList<ExpressionCompleteSuggestion> BuildCalcSuggestions(
        ExpressionCompleteContext ctx)
    {
        var functions = BuiltInFunctionCatalog.All;

        var varItems = ctx.VariableSource.GetVariableNames()
            .Concat(BuiltInFunctionCatalog.BuiltInOutputVars)
            .Distinct(StringComparer.Ordinal)
            .Select(name => new ExpressionCompleteSuggestion
            {
                DisplayText   = name,
                InsertText    = name,
                Icon          = "\uE74B",
                TypeHint      = "variable",
                Documentation = $"Variable: {name}",
                SortPriority  = 20,
            });

        return FilterAndSort([.. functions, .. varItems], ctx.Token);
    }

    // ── Fuzzy filter + sort ───────────────────────────────────────────────────

    private static IReadOnlyList<ExpressionCompleteSuggestion> FilterAndSort(
        IEnumerable<ExpressionCompleteSuggestion> source,
        string token)
    {
        var result = new List<ExpressionCompleteSuggestion>();

        foreach (var s in source)
        {
            int score = ExpressionFuzzyScorer.Score(token, s.DisplayText, out var indices);
            if (score < 0) continue;

            // Assign to avoid mutation of shared static items
            var copy = new ExpressionCompleteSuggestion
            {
                DisplayText   = s.DisplayText,
                InsertText    = s.InsertText,
                Icon          = s.Icon,
                TypeHint      = s.TypeHint,
                Documentation = s.Documentation,
                SortPriority  = s.SortPriority,
                CursorOffset  = s.CursorOffset,
                MatchScore    = score,
                MatchedIndices = indices.Count > 0 ? indices : null,
            };
            result.Add(copy);
        }

        result.Sort((a, b) =>
        {
            int cmp = b.MatchScore.CompareTo(a.MatchScore);
            return cmp != 0 ? cmp : a.SortPriority.CompareTo(b.SortPriority);
        });

        return result;
    }
}
