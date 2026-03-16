// ==========================================================
// Project: WpfHexEditor.LSP
// File: Parsing/Lexer.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Tokenises document lines according to a LanguageDefinition.
//     Each call to TokenizeLine() applies the language's rules (regex
//     patterns) in order, producing an ordered non-overlapping token list.
//
// Architecture Notes:
//     Pattern: Strategy — Lexer is configured with a LanguageDefinition.
//     - Regex patterns are compiled and cached on first use (per rule).
//     - Handles block-comments across lines via IsInsideBlockComment state.
//     - Stateless per-line results; multi-line state carried via IsInsideBlockComment.
//     - IncrementalParser drives the Lexer; CodeEditor does not call it directly.
// ==========================================================

using System.Text.RegularExpressions;
using WpfHexEditor.LSP.Models;

namespace WpfHexEditor.LSP.Parsing;

/// <summary>
/// Line-by-line lexer driven by a <see cref="LanguageDefinition"/>.
/// </summary>
public sealed class Lexer
{
    private readonly LanguageDefinition _language;

    // Pre-compiled regex per rule index (lazy, thread-safe via Lazy<>).
    private readonly Lazy<Regex>[] _compiledRules;

    // Mapping from rule type string → TokenType.
    private static readonly Dictionary<string, TokenType> TypeMap
        = new(StringComparer.OrdinalIgnoreCase)
        {
            ["keyword"]     = TokenType.Keyword,
            ["identifier"]  = TokenType.Identifier,
            ["type"]        = TokenType.Type,
            ["number"]      = TokenType.Number,
            ["string"]      = TokenType.String,
            ["comment"]     = TokenType.Comment,
            ["operator"]    = TokenType.Operator,
            ["preprocessor"]= TokenType.Preprocessor,
            ["punctuation"] = TokenType.Punctuation,
        };

    // Multi-line block-comment state (carried between TokenizeLine calls).
    /// <summary><c>true</c> when the lexer is inside an open block comment.</summary>
    public bool IsInsideBlockComment { get; internal set; }

    public Lexer(LanguageDefinition language)
    {
        _language = language ?? throw new ArgumentNullException(nameof(language));
        _compiledRules = [.. language.Rules.Select(r =>
            new Lazy<Regex>(() => new Regex(r.Pattern,
                RegexOptions.Compiled | RegexOptions.CultureInvariant)))];
    }

    /// <summary>
    /// Resets block-comment tracking (call before a full document re-parse).
    /// </summary>
    public void Reset() => IsInsideBlockComment = false;

    /// <summary>
    /// Tokenises a single line and returns the ordered, non-overlapping token list.
    /// Updates <see cref="IsInsideBlockComment"/> for the next call.
    /// </summary>
    public IReadOnlyList<Token> TokenizeLine(string lineText, int lineIndex)
    {
        if (string.IsNullOrEmpty(lineText))
            return [];

        var covered  = new bool[lineText.Length]; // columns already assigned
        var tokens   = new List<Token>();

        // -- Handle open block comment carried from previous line -----------
        if (IsInsideBlockComment)
        {
            var endToken = TryConsumeBlockCommentEnd(lineText, 0, lineIndex, covered);
            if (endToken is not null)
            {
                tokens.Add(endToken);
                IsInsideBlockComment = false;
            }
            else
            {
                // Whole line is inside the block comment.
                tokens.Add(new Token(TokenType.Comment, lineText, 0, lineIndex));
                return tokens;
            }
        }

        // -- Inline line comment -------------------------------------------
        if (_language.LineComment is { Length: > 0 } lc)
        {
            int idx = FindUnquotedIndex(lineText, lc, covered);
            if (idx >= 0)
            {
                MarkCovered(covered, idx, lineText.Length - idx);
                tokens.Add(new Token(TokenType.Comment, lineText[idx..], idx, lineIndex));
            }
        }

        // -- Block comment open (may not close on this line) ----------------
        if (_language.BlockCommentStart is { Length: > 0 } bcs
            && _language.BlockCommentEnd  is { Length: > 0 } bce)
        {
            int bIdx = FindUnquotedIndex(lineText, bcs, covered);
            if (bIdx >= 0)
            {
                int endIdx = lineText.IndexOf(bce, bIdx + bcs.Length, StringComparison.Ordinal);
                if (endIdx >= 0)
                {
                    int len = endIdx + bce.Length - bIdx;
                    MarkCovered(covered, bIdx, len);
                    tokens.Add(new Token(TokenType.Comment, lineText.Substring(bIdx, len), bIdx, lineIndex));
                }
                else
                {
                    MarkCovered(covered, bIdx, lineText.Length - bIdx);
                    tokens.Add(new Token(TokenType.Comment, lineText[bIdx..], bIdx, lineIndex));
                    IsInsideBlockComment = true;
                }
            }
        }

        // -- Apply language rules -------------------------------------------
        for (int ri = 0; ri < _language.Rules.Count; ri++)
        {
            var rule  = _language.Rules[ri];
            var regex = _compiledRules[ri].Value;
            var type  = TypeMap.TryGetValue(rule.Type, out var t) ? t : TokenType.Unknown;

            foreach (Match m in regex.Matches(lineText))
            {
                if (!IsCovered(covered, m.Index, m.Length))
                {
                    MarkCovered(covered, m.Index, m.Length);
                    tokens.Add(new Token(type, m.Value, m.Index, lineIndex));
                }
            }
        }

        return [.. tokens.OrderBy(t => t.StartColumn)];
    }

    // -----------------------------------------------------------------------

    private Token? TryConsumeBlockCommentEnd(
        string text, int from, int lineIndex, bool[] covered)
    {
        if (_language.BlockCommentEnd is not { Length: > 0 } bce) return null;
        int idx = text.IndexOf(bce, from, StringComparison.Ordinal);
        if (idx < 0) return null;
        int len = idx + bce.Length - from;
        MarkCovered(covered, from, len);
        return new Token(TokenType.Comment, text.Substring(from, len), from, lineIndex);
    }

    private static int FindUnquotedIndex(string text, string needle, bool[] covered)
    {
        int idx = text.IndexOf(needle, StringComparison.Ordinal);
        while (idx >= 0)
        {
            if (!covered[idx]) return idx;
            idx = text.IndexOf(needle, idx + 1, StringComparison.Ordinal);
        }
        return -1;
    }

    private static void MarkCovered(bool[] covered, int start, int length)
    {
        int end = Math.Min(start + length, covered.Length);
        for (int i = start; i < end; i++) covered[i] = true;
    }

    private static bool IsCovered(bool[] covered, int start, int length)
    {
        int end = Math.Min(start + length, covered.Length);
        for (int i = start; i < end; i++)
            if (covered[i]) return true;
        return false;
    }
}
