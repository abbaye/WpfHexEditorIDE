// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Snippet.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-05
// Description:
//     Immutable data model for a code snippet. A snippet is expanded when the
//     user types its Trigger string and presses Tab.
//     The Body may contain the special token $cursor to mark where the caret
//     should land after expansion.
// ==========================================================

namespace WpfHexEditor.Editor.CodeEditor.Snippets;

/// <summary>
/// Immutable definition of a code snippet.
/// </summary>
/// <param name="Trigger">Short keyword the user types to expand this snippet (e.g. "for", "try").</param>
/// <param name="Body">Expanded text.  Use <c>$cursor</c> to mark caret placement; use <c>$tab</c>
///   for a literal tab character when the editor uses spaces.  Newlines are \n.</param>
/// <param name="Description">One-line description shown in completion lists.</param>
public sealed record Snippet(string Trigger, string Body, string Description)
{
    /// <summary>Token in <see cref="Body"/> replaced by the caret position after expansion.</summary>
    public const string CursorMarker = "$cursor";
}
