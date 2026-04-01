// ==========================================================
// Project: WpfHexEditor.Editor.TextEditor
// File: Highlighting/IContextualHighlighter.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-22
// Description:
//     Contract for stateful, multi-line syntax highlighters.
//     BuildContext() scans the full document snapshot and returns an opaque
//     context array that is then passed to each Highlight() call.
//     Both methods are safe to call from a background thread — no shared
//     mutable state is retained between calls.
// ==========================================================

namespace WpfHexEditor.Editor.TextEditor.Highlighting;

/// <summary>
/// Multi-line syntax highlighter that builds cross-line context before
/// processing individual lines.
/// </summary>
/// <remarks>
/// The two-step API is designed so that <see cref="BuildContext"/> can run
/// entirely on a background thread alongside <see cref="Highlight"/>.
/// No mutable state is shared between concurrent invocations.
/// </remarks>
internal interface IContextualHighlighter
{
    /// <summary>
    /// Scans the entire document snapshot and returns an opaque per-line
    /// context array (e.g. fence-language tags).
    /// </summary>
    /// <param name="allLines">All document lines at the time of the highlight pass.</param>
    /// <returns>
    /// A <c>string?[]</c> whose meaning is defined by the implementing type.
    /// Pass the returned value to every subsequent <see cref="Highlight"/> call
    /// in the same pass.
    /// </returns>
    string?[] BuildContext(IReadOnlyList<string> allLines);

    /// <summary>
    /// Returns syntax highlight spans for the given line using the context
    /// produced by <see cref="BuildContext"/>.
    /// </summary>
    /// <param name="lineText">Text content of the line.</param>
    /// <param name="lineIndex">Zero-based line index in the document.</param>
    /// <param name="context">Context array returned by <see cref="BuildContext"/>.</param>
    IReadOnlyList<ColoredSpan> Highlight(string lineText, int lineIndex, string?[] context);
}
