// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Models/CodeEditorUndoEntry.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-18
// Description:
//     Concrete IUndoEntry for the CodeEditor. Holds the data needed to
//     apply or invert a text mutation on a CodeDocument.
//     Implements VS-style character coalescing via TryMerge.
//
// Architecture Notes:
//     Pattern: Command Value Object + Coalescing Strategy (via TryMerge)
//     Replaces TextEdit + UndoRedoStack (deleted file Models/UndoRedoStack.cs).
//     Coalescing: consecutive Insert entries at adjacent columns within 500 ms
//     and without word-break characters are merged into a single undo step,
//     matching Visual Studio / VS Code word-granularity undo behavior.
// ==========================================================

using System;
using System.Diagnostics.CodeAnalysis;
using WpfHexEditor.Editor.Core.Undo;

namespace WpfHexEditor.Editor.CodeEditor.Models;

/// <summary>Kind of text mutation recorded by the CodeEditor.</summary>
public enum CodeEditKind { Insert, Delete, Replace, NewLine, DeleteLine }

/// <summary>
/// A single undoable text mutation recorded by the CodeEditor.
/// Supports VS-style character coalescing: consecutive insertions of
/// non-word-break characters at adjacent positions within a 500 ms window
/// are merged into a single undo step.
/// </summary>
public sealed class CodeEditorUndoEntry : IUndoEntry
{
    private const double CoalesceWindowMs = 500.0;

    public CodeEditKind Kind     { get; }
    public TextPosition Position { get; }
    public string       Text     { get; }

    public string   Description { get; }
    public long     Revision    { get; set; }
    public DateTime Timestamp   { get; }

    public CodeEditorUndoEntry(
        CodeEditKind kind,
        TextPosition position,
        string       text,
        DateTime?    timestamp = null)
    {
        Kind        = kind;
        Position    = position;
        Text        = text;
        Timestamp   = timestamp ?? DateTime.UtcNow;
        Description = BuildDescription(kind, text);
    }

    // ------------------------------------------------------------------
    // Coalescing
    // ------------------------------------------------------------------

    public bool TryMerge(IUndoEntry next, [NotNullWhen(true)] out IUndoEntry? merged)
    {
        merged = null;

        // Only Insert+Insert can be coalesced.
        if (Kind != CodeEditKind.Insert)           return false;
        if (next is not CodeEditorUndoEntry n)     return false;
        if (n.Kind != CodeEditKind.Insert)         return false;

        // Must be within the coalescing time window.
        if ((n.Timestamp - Timestamp).TotalMilliseconds > CoalesceWindowMs) return false;

        // New character must immediately follow the current word on the same line.
        bool adjacent = n.Position.Line   == Position.Line
                     && n.Position.Column == Position.Column + Text.Length;
        if (!adjacent) return false;

        // Word-break characters reset coalescing (matches VS/VSCode behavior).
        if (ContainsWordBreak(Text) || ContainsWordBreak(n.Text)) return false;

        merged = new CodeEditorUndoEntry(
            CodeEditKind.Insert,
            Position,
            Text + n.Text,
            Timestamp);  // Preserve the original start timestamp of the word

        merged.Revision = n.Revision;
        return true;
    }

    // ------------------------------------------------------------------
    // Private helpers
    // ------------------------------------------------------------------

    private static bool ContainsWordBreak(string text)
    {
        foreach (char c in text)
            if (char.IsWhiteSpace(c) || char.IsPunctuation(c) || char.IsSymbol(c))
                return true;
        return false;
    }

    private static string BuildDescription(CodeEditKind kind, string text) => kind switch
    {
        CodeEditKind.Insert     => text.Length == 1
                                       ? $"Typed '{text}'"
                                       : $"Typed '{Truncate(text, 20)}'",
        CodeEditKind.Delete     => "Delete",
        CodeEditKind.Replace    => "Replace",
        CodeEditKind.NewLine    => "New Line",
        CodeEditKind.DeleteLine => "Delete Line",
        _                       => "Edit"
    };

    private static string Truncate(string s, int max)
        => s.Length <= max ? s : s[..max] + "\u2026";  // "…"
}
