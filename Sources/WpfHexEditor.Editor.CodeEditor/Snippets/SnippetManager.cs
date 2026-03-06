// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: SnippetManager.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-05
// Description:
//     Registry and expansion engine for code snippets.
//     Register snippets via Register(); the CodeEditor calls TryExpand()
//     on Tab to check whether the word immediately left of the caret is a trigger.
//
// Architecture Notes:
//     Repository Pattern — acts as an in-memory store for Snippet objects,
//     keyed on their Trigger string for O(1) lookup.
//     Injectable via CodeEditor.SnippetManager DP so factories can supply
//     language-specific snippet sets at runtime.
// ==========================================================

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace WpfHexEditor.Editor.CodeEditor.Snippets;

/// <summary>
/// Manages a collection of <see cref="Snippet"/> objects and expands them
/// into a <see cref="SnippetExpansion"/> that the editor can apply.
/// </summary>
public sealed class SnippetManager
{
    private readonly Dictionary<string, Snippet> _snippets
        = new(StringComparer.Ordinal);

    #region Registration

    /// <summary>Adds or replaces a snippet.</summary>
    public void Register(Snippet snippet)
    {
        ArgumentNullException.ThrowIfNull(snippet);
        _snippets[snippet.Trigger] = snippet;
    }

    /// <summary>Registers a collection of snippets in bulk.</summary>
    public void Register(IEnumerable<Snippet> snippets)
    {
        foreach (var s in snippets)
            Register(s);
    }

    /// <summary>Removes a snippet by trigger.</summary>
    public bool Unregister(string trigger) => _snippets.Remove(trigger);

    /// <summary>All currently registered snippets (read-only view).</summary>
    public IReadOnlyDictionary<string, Snippet> Snippets => _snippets;

    #endregion

    #region Expansion

    /// <summary>
    /// Checks whether <paramref name="word"/> matches a registered trigger.
    /// </summary>
    /// <param name="word">Text immediately to the left of the caret (non-whitespace).</param>
    /// <param name="snippet">Matched snippet, or <c>null</c>.</param>
    /// <returns><c>true</c> if a snippet was found for this trigger.</returns>
    public bool TryExpand(string word, [NotNullWhen(true)] out Snippet? snippet)
        => _snippets.TryGetValue(word, out snippet);

    /// <summary>
    /// Builds a <see cref="SnippetExpansion"/> for the given snippet at the specified position.
    /// The trigger text (length = <paramref name="triggerLength"/>) has already been removed
    /// from the document — the expansion should be inserted at <paramref name="insertLine"/>,
    /// column <paramref name="insertColumn"/>.
    /// </summary>
    /// <param name="snippet">Snippet to expand.</param>
    /// <param name="insertLine">0-based line where the expansion starts.</param>
    /// <param name="insertColumn">0-based column where the expansion starts.</param>
    /// <param name="triggerLength">Character count of the trigger that was deleted.</param>
    /// <returns>Descriptor consumed by the editor to apply the text change and move the caret.</returns>
    public static SnippetExpansion BuildExpansion(
        Snippet snippet, int insertLine, int insertColumn, int triggerLength)
    {
        string body = snippet.Body;

        // Locate $cursor marker and remove it from the body text.
        int cursorMarkerPos = body.IndexOf(Snippet.CursorMarker, StringComparison.Ordinal);
        string expandedText;
        int cursorOffsetInText;

        if (cursorMarkerPos >= 0)
        {
            expandedText       = body.Remove(cursorMarkerPos, Snippet.CursorMarker.Length);
            cursorOffsetInText = cursorMarkerPos;
        }
        else
        {
            expandedText       = body;
            cursorOffsetInText = body.Length; // place caret at end
        }

        // Compute final caret position (line + column) inside expandedText.
        var (caretLine, caretCol) = OffsetToLineCol(expandedText, cursorOffsetInText, insertLine, insertColumn);

        return new SnippetExpansion(
            InsertLine:    insertLine,
            InsertColumn:  insertColumn,
            TriggerLength: triggerLength,
            ExpandedText:  expandedText,
            CaretLine:     caretLine,
            CaretColumn:   caretCol);
    }

    #endregion

    #region Helpers

    private static (int line, int col) OffsetToLineCol(
        string text, int charOffset, int baseLine, int baseCol)
    {
        int line = baseLine;
        int col  = baseCol;

        for (int i = 0; i < charOffset && i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                line++;
                col = 0;
            }
            else
            {
                col++;
            }
        }

        return (line, col);
    }

    #endregion
}

/// <summary>
/// Result produced by <see cref="SnippetManager.BuildExpansion"/> and consumed by
/// <see cref="Controls.CodeEditor"/> to apply the text change and position the caret.
/// </summary>
public sealed record SnippetExpansion(
    int    InsertLine,
    int    InsertColumn,
    int    TriggerLength,
    string ExpandedText,
    int    CaretLine,
    int    CaretColumn);
