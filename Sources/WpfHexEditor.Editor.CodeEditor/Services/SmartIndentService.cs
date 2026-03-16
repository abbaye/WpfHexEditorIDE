// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Services/SmartIndentService.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Computes the auto-indent string that should be inserted after the
//     user presses Enter at a given position in the document. Supports
//     both brace-style (C#/JS/Java/…) and indent-style (Python/YAML/…)
//     heuristics, controlled by IndentStrategy.
//
// Architecture Notes:
//     Pattern: Strategy (IndentStrategy enum selects algorithm)
//     - Pure C#, no WPF dependency — testable in isolation.
//     - CodeEditor calls ComputeIndent() in its key-down handler for Enter.
//     - Tab character vs. spaces is controlled by UseSpaces / TabSize.
// ==========================================================

namespace WpfHexEditor.Editor.CodeEditor.Services;

/// <summary>Selects the indent computation algorithm.</summary>
public enum IndentStrategy
{
    /// <summary>Match the indentation of the current line (copy whitespace prefix).</summary>
    CopyIndent,

    /// <summary>
    /// Copy indent + add one level when the line ends with an open brace <c>{</c>,
    /// then de-indent when the next non-empty character is a closing brace <c>}</c>.
    /// Suitable for C#, Java, JavaScript, CSS, …
    /// </summary>
    BraceIndent,

    /// <summary>
    /// Copy indent + add one level when the line ends with <c>:</c>
    /// (e.g. Python <c>if</c>, <c>def</c>, <c>for</c>, …).
    /// </summary>
    ColonIndent,
}

/// <summary>
/// Computes the auto-indent string inserted after the user presses Enter.
/// </summary>
public sealed class SmartIndentService
{
    // -- Options ----------------------------------------------------------

    /// <summary>Active indentation strategy. Default: <see cref="IndentStrategy.BraceIndent"/>.</summary>
    public IndentStrategy Strategy { get; set; } = IndentStrategy.BraceIndent;

    /// <summary>Number of spaces per indent level. Default: 4.</summary>
    public int TabSize { get; set; } = 4;

    /// <summary>When <c>true</c> indent with spaces; otherwise with a tab character.</summary>
    public bool UseSpaces { get; set; } = true;

    // -- Public API -------------------------------------------------------

    /// <summary>
    /// Returns the whitespace string that should be inserted at the beginning of
    /// the new line after the user presses Enter at <paramref name="caretOffset"/>
    /// within <paramref name="text"/>.
    /// </summary>
    /// <param name="text">Current flat document text.</param>
    /// <param name="caretOffset">Zero-based character offset of the caret (where Enter was pressed).</param>
    public string ComputeIndent(string text, int caretOffset)
    {
        if (string.IsNullOrEmpty(text) || caretOffset <= 0)
            return string.Empty;

        // Find the start of the current line.
        int lineStart = FindLineStart(text, caretOffset - 1);

        // Extract leading whitespace of the current line.
        string currentIndent = ExtractLeadingWhitespace(text, lineStart);

        return Strategy switch
        {
            IndentStrategy.BraceIndent  => ComputeBraceIndent(text, caretOffset, currentIndent),
            IndentStrategy.ColonIndent  => ComputeColonIndent(text, caretOffset, currentIndent),
            _                           => currentIndent,   // CopyIndent
        };
    }

    // -- Private ----------------------------------------------------------

    private string ComputeBraceIndent(string text, int caretOffset, string currentIndent)
    {
        // Find the last non-whitespace char before the caret on the current line.
        char lastChar = FindLastNonWhitespace(text, caretOffset - 1);

        if (lastChar == '{')
            return currentIndent + IndentUnit();

        // If the character immediately after (ignoring whitespace) is '}', de-indent one level.
        // This handles the case where the user pressed Enter between { and }.
        char nextChar = FindFirstNonWhitespaceAfter(text, caretOffset);
        if (nextChar == '}' && currentIndent.Length >= IndentUnit().Length)
            return currentIndent[..^IndentUnit().Length];

        return currentIndent;
    }

    private string ComputeColonIndent(string text, int caretOffset, string currentIndent)
    {
        char lastChar = FindLastNonWhitespace(text, caretOffset - 1);
        return lastChar == ':' ? currentIndent + IndentUnit() : currentIndent;
    }

    private static int FindLineStart(string text, int from)
    {
        for (int i = from; i >= 0; i--)
        {
            if (text[i] == '\n') return i + 1;
        }
        return 0;
    }

    private static string ExtractLeadingWhitespace(string text, int lineStart)
    {
        int end = lineStart;
        while (end < text.Length && (text[end] == ' ' || text[end] == '\t'))
            end++;
        return text[lineStart..end];
    }

    private static char FindLastNonWhitespace(string text, int fromIndex)
    {
        for (int i = Math.Min(fromIndex, text.Length - 1); i >= 0; i--)
        {
            char c = text[i];
            if (c == '\n' || c == '\r') break;
            if (c != ' ' && c != '\t') return c;
        }
        return '\0';
    }

    private static char FindFirstNonWhitespaceAfter(string text, int fromIndex)
    {
        for (int i = fromIndex; i < text.Length; i++)
        {
            char c = text[i];
            if (c != ' ' && c != '\t' && c != '\n' && c != '\r') return c;
        }
        return '\0';
    }

    private string IndentUnit()
        => UseSpaces ? new string(' ', TabSize) : "\t";
}
