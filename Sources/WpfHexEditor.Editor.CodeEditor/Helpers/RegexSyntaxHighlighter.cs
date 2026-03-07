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
//     This class is stateless across lines — no block-comment tracking.
//
// Architecture Notes:
//     Strategy Pattern — implements ISyntaxHighlighter.
//     Flyweight        — compiled Regex objects are shared; brushes are frozen.
// ==========================================================

using System.Text.RegularExpressions;
using System.Windows.Media;

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

    /// <param name="rules">
    /// Ordered rules applied to each line. First-match wins within each span.
    /// </param>
    public SyntaxRuleHighlighter(IEnumerable<RegexHighlightRule> rules)
    {
        _rules = rules.ToArray();
    }

    // -- ISyntaxHighlighter ---------------------------------------------------

    public IReadOnlyList<SyntaxHighlightToken> Highlight(string lineText, int lineIndex)
    {
        if (string.IsNullOrEmpty(lineText)) return [];

        var tokens = new List<SyntaxHighlightToken>();
        var covered = new bool[lineText.Length];  // tracks which columns already matched

        foreach (var rule in _rules)
        {
            foreach (Match match in rule.Regex.Matches(lineText))
            {
                if (match.Length == 0) continue;

                // Skip if any character in this span is already matched by an earlier rule.
                bool alreadyCovered = false;
                for (int i = match.Index; i < match.Index + match.Length; i++)
                {
                    if (covered[i]) { alreadyCovered = true; break; }
                }
                if (alreadyCovered) continue;

                for (int i = match.Index; i < match.Index + match.Length; i++)
                    covered[i] = true;

                tokens.Add(new SyntaxHighlightToken(
                    match.Index,
                    match.Length,
                    match.Value,
                    rule.Foreground,
                    rule.IsBold,
                    rule.IsItalic));
            }
        }

        return tokens;
    }

    public void Reset() { /* stateless — nothing to reset */ }
}

/// <summary>
/// A single syntax highlighting rule: a compiled regex + display attributes.
/// </summary>
public sealed class RegexHighlightRule
{
    public RegexHighlightRule(
        string  pattern,
        Brush   foreground,
        bool    isBold   = false,
        bool    isItalic = false,
        RegexOptions options = RegexOptions.None)
    {
        Regex      = new Regex(pattern, options | RegexOptions.Compiled);
        Foreground = foreground;
        IsBold     = isBold;
        IsItalic   = isItalic;
    }

    public Regex Regex      { get; }
    public Brush Foreground { get; }
    public bool  IsBold     { get; }
    public bool  IsItalic   { get; }
}
