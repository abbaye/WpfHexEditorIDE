// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Services/BracketMatchingService.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Locates matching bracket pairs in a text document for the given
//     caret position. Used by CodeEditor to highlight () [] {} pairs.
//     Bracket pairs are data-driven from LanguageDefinition.BracketPairs
//     (whfmt "bracketPairs" section). Falls back to common ()[]{}
//     when the language does not define explicit pairs.
//
// Architecture Notes:
//     Pattern: Service (stateful — language-aware)
//     - Call SetLanguage() when the active language changes.
//     - Input: flat text + caret offset.
//     - Returns the two match offsets or null when no match is found.
//     - Pure C#, no WPF dependency — testable in isolation.
// ==========================================================

using WpfHexEditor.Core.ProjectSystem.Languages;

namespace WpfHexEditor.Editor.CodeEditor.Services;

/// <summary>
/// Describes a bracket pair found in a text document.
/// Both offsets are zero-based character indices into the flat document text.
/// </summary>
public sealed record BracketMatch(int OpenOffset, int CloseOffset, char OpenChar, char CloseChar);

/// <summary>
/// Locates the matching bracket at or adjacent to a given caret position.
/// Bracket pairs are driven by whfmt "bracketPairs"; falls back to ()[]{}
/// when the active language does not define explicit pairs.
/// </summary>
public sealed class BracketMatchingService
{
    // Fallback pairs used when LanguageDefinition.BracketPairs is null.
    private static readonly IReadOnlyList<(char Open, char Close)> s_defaultPairs =
    [
        ('(', ')'),
        ('[', ']'),
        ('{', '}'),
    ];

    private Dictionary<char, char> _openToClose = BuildOpenToClose(null);
    private Dictionary<char, char> _closeToOpen = BuildCloseToOpen(null);

    /// <summary>
    /// Updates the active bracket pairs from the language definition.
    /// Call when the editor's active language changes.
    /// Pass <c>null</c> to revert to the default ()[]{}  fallback.
    /// </summary>
    public void SetLanguage(LanguageDefinition? language)
    {
        var pairs = language?.BracketPairs;
        _openToClose = BuildOpenToClose(pairs);
        _closeToOpen = BuildCloseToOpen(pairs);
    }

    // -- Public API -------------------------------------------------------

    /// <summary>
    /// Finds the matching bracket for the character at or immediately before
    /// <paramref name="caretOffset"/> in <paramref name="text"/>.
    /// Returns <c>null</c> when no bracket is under the caret.
    /// </summary>
    /// <param name="text">Flat document text.</param>
    /// <param name="caretOffset">Zero-based caret character offset.</param>
    public BracketMatch? FindMatch(string text, int caretOffset)
    {
        if (string.IsNullOrEmpty(text)) return null;

        // Try the char at offset, then the char just before.
        foreach (var offset in CandidateOffsets(caretOffset, text.Length))
        {
            char ch = text[offset];

            if (_openToClose.TryGetValue(ch, out char close))
                return FindForward(text, offset, ch, close);

            if (_closeToOpen.TryGetValue(ch, out char open))
                return FindBackward(text, offset, ch, open);
        }

        return null;
    }

    // -- Private ----------------------------------------------------------

    private static Dictionary<char, char> BuildOpenToClose(IReadOnlyList<(char Open, char Close)>? pairs)
    {
        var source = pairs ?? s_defaultPairs;
        var dict = new Dictionary<char, char>(source.Count);
        foreach (var (o, c) in source) dict[o] = c;
        return dict;
    }

    private static Dictionary<char, char> BuildCloseToOpen(IReadOnlyList<(char Open, char Close)>? pairs)
    {
        var source = pairs ?? s_defaultPairs;
        var dict = new Dictionary<char, char>(source.Count);
        foreach (var (o, c) in source) dict[c] = o;
        return dict;
    }

    private static IEnumerable<int> CandidateOffsets(int caretOffset, int textLength)
    {
        if (caretOffset >= 0 && caretOffset < textLength)
            yield return caretOffset;
        if (caretOffset > 0 && caretOffset - 1 < textLength)
            yield return caretOffset - 1;
    }

    /// <summary>Scan forward from <paramref name="openOffset"/> to find matching close bracket.</summary>
    private static BracketMatch? FindForward(string text, int openOffset, char openChar, char closeChar)
    {
        int depth = 1;
        for (int i = openOffset + 1; i < text.Length; i++)
        {
            char c = text[i];
            if      (c == openChar)  depth++;
            else if (c == closeChar)
            {
                depth--;
                if (depth == 0)
                    return new BracketMatch(openOffset, i, openChar, closeChar);
            }
        }
        return null;  // Unmatched open bracket.
    }

    /// <summary>Scan backward from <paramref name="closeOffset"/> to find matching open bracket.</summary>
    private static BracketMatch? FindBackward(string text, int closeOffset, char closeChar, char openChar)
    {
        int depth = 1;
        for (int i = closeOffset - 1; i >= 0; i--)
        {
            char c = text[i];
            if      (c == closeChar) depth++;
            else if (c == openChar)
            {
                depth--;
                if (depth == 0)
                    return new BracketMatch(i, closeOffset, openChar, closeChar);
            }
        }
        return null;  // Unmatched close bracket.
    }
}
