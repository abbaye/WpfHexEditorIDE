// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: ISyntaxHighlighter.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-05
// Description:
//     Contract for line-by-line syntax highlighters used by CodeEditor.
//     Both the built-in JSON highlighter and regex-based language highlighters
//     implement this interface, enabling pluggable syntax highlighting.
//
// Architecture Notes:
//     Strategy Pattern — CodeEditor holds an ISyntaxHighlighter; swap at runtime
//     to change language without rebuilding the editor control.
// ==========================================================

using System.Windows.Media;
using WpfHexEditor.Core.ProjectSystem.Languages;

namespace WpfHexEditor.Editor.CodeEditor.Helpers;

/// <summary>
/// Contract for a syntax highlighter that tokenises one line at a time
/// and returns a list of coloured segments for <see cref="Controls.CodeEditor"/> to render.
/// </summary>
public interface ISyntaxHighlighter
{
    /// <summary>
    /// Human-readable language name shown in the IDE status bar (e.g. "JSON", "C#", "Python").
    /// Returns <c>null</c> when the language name is not applicable or unknown.
    /// </summary>
    string? LanguageName { get; }

    /// <summary>
    /// Tokenises a single line of text.
    /// </summary>
    /// <param name="lineText">Raw text of the line (no trailing newline).</param>
    /// <param name="lineIndex">0-based line index — used by stateful highlighters (e.g. JSON context).</param>
    /// <returns>Ordered, non-overlapping tokens to render. Empty list for empty input.</returns>
    IReadOnlyList<SyntaxHighlightToken> Highlight(string lineText, int lineIndex);

    /// <summary>
    /// Resets any multi-line parsing state (e.g. bracket nesting, block-comment tracking).
    /// Called before a full document re-render begins.
    /// </summary>
    void Reset();
}

/// <summary>
/// A coloured text segment within a single line, produced by an <see cref="ISyntaxHighlighter"/>.
/// </summary>
/// <param name="StartColumn">0-based column where this token starts.</param>
/// <param name="Length">Character count of this token.</param>
/// <param name="Text">Substring of the line covered by this token.</param>
/// <param name="Foreground">Brush to paint the text.</param>
/// <param name="IsBold">Whether to render the token in bold.</param>
/// <param name="IsItalic">Whether to render the token in italic.</param>
public readonly record struct SyntaxHighlightToken(
    int             StartColumn,
    int             Length,
    string          Text,
    Brush           Foreground,
    bool            IsBold   = false,
    bool            IsItalic = false,
    SyntaxTokenKind Kind     = SyntaxTokenKind.Default);
