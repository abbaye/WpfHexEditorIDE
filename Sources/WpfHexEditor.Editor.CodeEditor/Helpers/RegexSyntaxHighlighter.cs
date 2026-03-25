// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Helpers/SyntaxRuleHighlighter.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-05
// Description:
//     Generic regex-based implementation of ISyntaxHighlighter.
//     Each rule is a compiled regex + an associated brush/style.
//     Rules are applied in order; the first match wins for each span.
//     Tracks multi-line block comments using blockCommentStart/blockCommentEnd delimiters
//     passed from LanguageDefinition (populated from the .whlang file).
//
// Architecture Notes:
//     Strategy Pattern — implements ISyntaxHighlighter.
//     Flyweight        — compiled Regex objects are shared; brushes are frozen.
// ==========================================================

using System.Text.RegularExpressions;
using System.Windows.Media;
using WpfHexEditor.Core.ProjectSystem.Languages;

namespace WpfHexEditor.Editor.CodeEditor.Helpers;

/// <summary>
/// Applies an ordered list of regex rules to each source line and emits
/// <see cref="SyntaxHighlightToken"/>s for the first match in each span.
/// Named <c>SyntaxRuleHighlighter</c> to avoid ambiguity with
/// <see cref="WpfHexEditor.Editor.TextEditor.Highlighting.RegexSyntaxHighlighter"/>.
/// </summary>
public sealed class SyntaxRuleHighlighter : ISyntaxHighlighter
{
    private readonly IReadOnlyList<RegexHighlightRule> _rules;
    private readonly string?                            _languageName;

    // Block-comment state — driven by blockCommentStart/blockCommentEnd from the .whlang file.
    private readonly string? _blockCommentStart;
    private readonly string? _blockCommentEnd;
    private bool             _inBlockComment;

    // Comment brush: resolved from the first Comment-kind rule (if any), used for block-comment spans.
    private readonly Brush _commentBrush;

    /// <param name="rules">Ordered rules applied to each line. First-match wins within each span.</param>
    /// <param name="languageName">Human-readable language name for the IDE status bar (e.g. "C#").</param>
    /// <param name="blockCommentStart">Opening block-comment delimiter (e.g. "/*" or "&lt;!--").</param>
    /// <param name="blockCommentEnd">Closing block-comment delimiter (e.g. "*/" or "--&gt;").</param>
    public SyntaxRuleHighlighter(
        IEnumerable<RegexHighlightRule> rules,
        string?                         languageName       = null,
        string?                         blockCommentStart  = null,
        string?                         blockCommentEnd    = null)
    {
        _rules              = rules.ToArray();
        _languageName       = languageName;
        _blockCommentStart  = blockCommentStart;
        _blockCommentEnd    = blockCommentEnd;
        _commentBrush       = _rules.FirstOrDefault(r => r.Kind == SyntaxTokenKind.Comment)?.Foreground
                              ?? new SolidColorBrush(Color.FromRgb(0x6A, 0x99, 0x55));
    }

    // -- ISyntaxHighlighter ---------------------------------------------------

    /// <inheritdoc />
    public string? LanguageName => _languageName;

    public IReadOnlyList<SyntaxHighlightToken> Highlight(string lineText, int lineIndex)
    {
        if (string.IsNullOrEmpty(lineText)) return [];

        // --- Continuation: inside an open block comment from a previous line ---
        if (_inBlockComment && _blockCommentEnd != null)
        {
            int closeIdx = lineText.IndexOf(_blockCommentEnd, StringComparison.Ordinal);

            if (closeIdx < 0)
                return [new SyntaxHighlightToken(0, lineText.Length, lineText, _commentBrush, IsItalic: true, Kind: SyntaxTokenKind.Comment)];

            int closeEnd    = closeIdx + _blockCommentEnd.Length;
            _inBlockComment = false;

            var result = new List<SyntaxHighlightToken>();
            result.Add(new SyntaxHighlightToken(0, closeEnd, lineText[..closeEnd], _commentBrush, IsItalic: true, Kind: SyntaxTokenKind.Comment));

            if (closeEnd < lineText.Length)
                result.AddRange(HighlightFragment(lineText[closeEnd..], closeEnd));

            return result;
        }

        // --- Check whether a block comment opens on this line ---
        if (_blockCommentStart != null)
        {
            int openIdx = lineText.IndexOf(_blockCommentStart, StringComparison.Ordinal);

            if (openIdx >= 0)
            {
                int closeIdx = _blockCommentEnd != null
                    ? lineText.IndexOf(_blockCommentEnd, openIdx + _blockCommentStart.Length, StringComparison.Ordinal)
                    : -1;

                if (closeIdx < 0)
                {
                    // Multi-line block comment opens here — no closing delimiter on this line.
                    _inBlockComment = true;

                    var result = new List<SyntaxHighlightToken>();
                    if (openIdx > 0)
                        result.AddRange(HighlightFragment(lineText[..openIdx], 0));

                    result.Add(new SyntaxHighlightToken(
                        openIdx, lineText.Length - openIdx, lineText[openIdx..],
                        _commentBrush, IsItalic: true, Kind: SyntaxTokenKind.Comment));

                    return result;
                }

                // Both opening and closing on the same line — the Comment regex rule handles it; fall through.
            }
        }

        // --- Normal line: run all regex rules ---
        return HighlightFragment(lineText, 0);
    }

    /// <inheritdoc />
    public void Reset() { _inBlockComment = false; }

    // -- Private helpers ------------------------------------------------------

    /// <summary>
    /// Applies all regex rules to <paramref name="text"/> and returns tokens with
    /// column positions offset by <paramref name="columnOffset"/>.
    /// </summary>
    private IReadOnlyList<SyntaxHighlightToken> HighlightFragment(string text, int columnOffset)
    {
        if (string.IsNullOrEmpty(text)) return [];

        var tokens  = new List<SyntaxHighlightToken>();
        var covered = new bool[text.Length];

        foreach (var rule in _rules)
        {
            foreach (Match match in rule.Regex.Matches(text))
            {
                if (match.Length == 0) continue;

                bool alreadyCovered = false;
                for (int i = match.Index; i < match.Index + match.Length; i++)
                {
                    if (covered[i]) { alreadyCovered = true; break; }
                }
                if (alreadyCovered) continue;

                for (int i = match.Index; i < match.Index + match.Length; i++)
                    covered[i] = true;

                tokens.Add(new SyntaxHighlightToken(
                    columnOffset + match.Index,
                    match.Length,
                    match.Value,
                    rule.Foreground,
                    rule.IsBold,
                    rule.IsItalic,
                    rule.Kind));
            }
        }

        return tokens;
    }
}

/// <summary>
/// A single syntax highlighting rule: a compiled regex + display attributes.
/// </summary>
public sealed class RegexHighlightRule
{
    public RegexHighlightRule(
        string          pattern,
        Brush           foreground,
        bool            isBold   = false,
        bool            isItalic = false,
        RegexOptions    options  = RegexOptions.None,
        SyntaxTokenKind kind     = SyntaxTokenKind.Default)
    {
        Regex      = new Regex(pattern, options | RegexOptions.Compiled);
        Foreground = foreground;
        IsBold     = isBold;
        IsItalic   = isItalic;
        Kind       = kind;
    }

    public Regex           Regex      { get; }
    public Brush           Foreground { get; }
    public bool            IsBold     { get; }
    public bool            IsItalic   { get; }
    public SyntaxTokenKind Kind       { get; }
}
