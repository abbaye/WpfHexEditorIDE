// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Services/BracketDepthColorizer.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-30
// Description:
//     Stateful depth tracker that assigns CE_Bracket_1/2/3/4 colors to bracket
//     characters based on their nesting depth within the current document.
//     Bracket pairs are data-driven from LanguageDefinition.BracketPairs (whfmt).
//     Null BracketPairs → service is a no-op (no depth tracking).
//
// Architecture Notes:
//     Service (stateful) — one instance per CodeEditor.
//     Call SetPairs() when the active language changes.
//     Call Reset() + pre-scan non-visible lines before each render pass.
//     Call ColorizeLine() for each rendered line in document order.
//
//     Design: scans lineText directly for bracket chars — does NOT rely on
//     any SyntaxTokenKind.Bracket tokens being present in the token stream.
//     String/comment token spans are used to skip brackets inside them (correct
//     depth tracking, no erroneous colorization of brackets in string literals).
//
// Depth color palette (4 levels, cycling):
//     depth % 4 == 0 → CE_Bracket_1  (gold / yellow family)
//     depth % 4 == 1 → CE_Bracket_2  (pink / magenta family)
//     depth % 4 == 2 → CE_Bracket_3  (blue / teal family)
//     depth % 4 == 3 → CE_Bracket_4  (green / lime family)
// ==========================================================

using System;
using System.Collections.Generic;
using System.Windows.Media;
using WpfHexEditor.Core.ProjectSystem.Languages;
using WpfHexEditor.Editor.CodeEditor.Helpers;

namespace WpfHexEditor.Editor.CodeEditor.Services;

/// <summary>
/// Assigns depth-based foreground colors to bracket characters using whfmt-driven bracket pairs.
/// Nil-safe: when no pairs are configured the service returns tokens unchanged.
/// </summary>
internal sealed class BracketDepthColorizer
{
    // -- Depth color resource keys (CE_Bracket_N defined in all Colors.xaml) --
    private static readonly string[] s_depthKeys =
    [
        "CE_Bracket_1",
        "CE_Bracket_2",
        "CE_Bracket_3",
        "CE_Bracket_4",
    ];

    // -- Per-bracket-open-char depth counter --
    // Key = open char (e.g. '('), Value = current nesting depth.
    private readonly Dictionary<char, int> _depths = new();

    // -- Reverse lookup: close char → matching open char --
    private Dictionary<char, char> _closeToOpen = new();

    // -- Fast membership test for all bracket chars --
    private HashSet<char> _bracketChars = new();

    private bool _hasPairs;

    // -- Public API -----------------------------------------------------------

    /// <summary>
    /// Configures the bracket pair set for the active language.
    /// Pass <c>null</c> to disable depth colorization (no pairs = no depth info).
    /// </summary>
    public void SetPairs(IReadOnlyList<(char Open, char Close)>? pairs)
    {
        _depths.Clear();
        _closeToOpen = new Dictionary<char, char>();
        _bracketChars = new HashSet<char>();
        _hasPairs = pairs is { Count: > 0 };

        if (!_hasPairs) return;

        foreach (var (o, c) in pairs!)
        {
            _depths[o] = 0;
            _closeToOpen[c] = o;
            _bracketChars.Add(o);
            _bracketChars.Add(c);
        }
    }

    /// <summary>
    /// Resets all nesting depths to zero.
    /// Call before scanning from the beginning of the document.
    /// </summary>
    public void Reset()
    {
        foreach (var key in _depths.Keys.ToArrayFast())
            _depths[key] = 0;
    }

    /// <summary>
    /// Advances bracket depth state for a non-rendered line (before the visible range).
    /// Uses <paramref name="cachedTokens"/> to skip brackets inside string/comment spans.
    /// Falls back to a raw character scan when no token cache is available.
    /// </summary>
    public void AdvanceLine(
        string                               lineText,
        IReadOnlyList<SyntaxHighlightToken>? cachedTokens)
    {
        if (!_hasPairs || string.IsNullOrEmpty(lineText)) return;

        if (cachedTokens is { Count: > 0 })
        {
            // Build protected-column set from string/comment token spans.
            // Brackets inside strings/comments advance depth but are not colorized.
            var protectedCols = BuildProtectedCols(lineText.Length, cachedTokens);
            for (int i = 0; i < lineText.Length; i++)
            {
                if (_bracketChars.Contains(lineText[i]) && !protectedCols[i])
                    UpdateDepth(lineText[i]);
            }
        }
        else
        {
            // Token cache not yet available — scan raw text.
            // Brackets inside string literals may cause slight inaccuracies,
            // but the background pipeline will soon populate the cache.
            foreach (char ch in lineText)
                UpdateDepth(ch);
        }
    }

    /// <summary>
    /// Returns the token list with bracket characters assigned CE_Bracket_N foreground
    /// based on nesting depth.  Advances the depth state for this line.
    /// </summary>
    /// <remarks>
    /// Scans <paramref name="lineText"/> directly — does NOT require
    /// <see cref="SyntaxTokenKind.Bracket"/> tokens in <paramref name="tokens"/>.
    /// Brackets inside string/comment token spans are depth-tracked but not colorized.
    /// For brackets with a matching single-char token in the stream, the token's
    /// foreground is replaced in-place.  For brackets with no existing token, a new
    /// single-char token is injected at the end of the stream.
    /// </remarks>
    public IEnumerable<SyntaxHighlightToken> ColorizeLine(
        string                              lineText,
        IEnumerable<SyntaxHighlightToken>   tokens,
        Func<string, Brush?>                resourceLookup)
    {
        if (!_hasPairs)
        {
            foreach (var t in tokens) yield return t;
            yield break;
        }

        // Always materialize to a fresh list — we mutate entries in-place
        // (struct with { ... }) and must not modify the caller's collection.
        var tokenList = new List<SyntaxHighlightToken>(tokens);

        int len = lineText.Length;

        // Mark columns covered by string/comment tokens (brackets inside = advance only, no color).
        // Mark all columns covered by ANY token (brackets with a token = replace foreground).
        var protectedCols = BuildProtectedCols(len, tokenList);
        var coveredCols   = BuildCoveredCols(len, tokenList);

        // column → index in tokenList (only for single-char tokens at that exact column).
        var singleCharTokenAtCol = new Dictionary<int, int>(16);
        for (int ti = 0; ti < tokenList.Count; ti++)
        {
            var t = tokenList[ti];
            if (t.Length == 1 && t.StartColumn < len)
                singleCharTokenAtCol[t.StartColumn] = ti;
        }

        // Scan lineText: assign depth colors for each bracket char.
        var injected = new List<SyntaxHighlightToken>(4);

        for (int i = 0; i < len; i++)
        {
            char ch = lineText[i];
            if (!_bracketChars.Contains(ch)) continue;

            if (protectedCols[i])
            {
                // Inside string/comment — track depth but don't colorize.
                UpdateDepth(ch);
                continue;
            }

            int depth = ComputeDepthAndAdvance(ch);
            var brush = resourceLookup(s_depthKeys[depth % 4]);
            if (brush is null) continue;

            if (singleCharTokenAtCol.TryGetValue(i, out int ti))
            {
                // Replace foreground of the existing single-char token.
                tokenList[ti] = tokenList[ti] with { Foreground = brush };
            }
            else if (!coveredCols[i])
            {
                // No token covers this column — inject a new bracket token.
                injected.Add(new SyntaxHighlightToken(
                    i, 1, ch.ToString(), brush,
                    Kind: SyntaxTokenKind.Bracket));
            }
            // else: column is covered by a multi-char token — leave it as-is.
        }

        foreach (var t in tokenList) yield return t;
        foreach (var t in injected)  yield return t;
    }

    // -- Private helpers ------------------------------------------------------

    /// <summary>Returns a bool array where true = column is inside a string or comment token.</summary>
    private static bool[] BuildProtectedCols(int lineLen, IEnumerable<SyntaxHighlightToken> tokens)
    {
        var result = new bool[lineLen];
        foreach (var t in tokens)
        {
            if (t.Kind is not (SyntaxTokenKind.String or SyntaxTokenKind.Comment)) continue;
            int end = Math.Min(t.StartColumn + t.Length, lineLen);
            for (int c = t.StartColumn; c < end; c++)
                result[c] = true;
        }
        return result;
    }

    /// <summary>Returns a bool array where true = column is covered by any token.</summary>
    private static bool[] BuildCoveredCols(int lineLen, IEnumerable<SyntaxHighlightToken> tokens)
    {
        var result = new bool[lineLen];
        foreach (var t in tokens)
        {
            int end = Math.Min(t.StartColumn + t.Length, lineLen);
            for (int c = t.StartColumn; c < end; c++)
                result[c] = true;
        }
        return result;
    }

    private void UpdateDepth(char ch)
    {
        if (_depths.TryGetValue(ch, out int d))
            _depths[ch] = d + 1;
        else if (_closeToOpen.TryGetValue(ch, out char open) && _depths.TryGetValue(open, out int cd))
            _depths[open] = Math.Max(0, cd - 1);
    }

    /// <summary>
    /// Returns the depth index to use for <paramref name="ch"/> and advances the state.
    /// Open bracket: returns current depth, then increments.
    /// Close bracket: decrements first, returns new depth.
    /// </summary>
    private int ComputeDepthAndAdvance(char ch)
    {
        if (_depths.TryGetValue(ch, out int openDepth))
        {
            _depths[ch] = openDepth + 1;
            return openDepth;
        }

        if (_closeToOpen.TryGetValue(ch, out char open) && _depths.TryGetValue(open, out int closeDepth))
        {
            int newDepth = Math.Max(0, closeDepth - 1);
            _depths[open] = newDepth;
            return newDepth;
        }

        return 0;
    }
}

/// <summary>Extension shim for <see cref="Dictionary{TKey,TValue}.Keys"/> → array.</summary>
file static class DictExt
{
    public static TKey[] ToArrayFast<TKey, TVal>(this Dictionary<TKey, TVal>.KeyCollection keys)
        where TKey : notnull
    {
        var arr = new TKey[keys.Count];
        int i = 0;
        foreach (var k in keys) arr[i++] = k;
        return arr;
    }
}
