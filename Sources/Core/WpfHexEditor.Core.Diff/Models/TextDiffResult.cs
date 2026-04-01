// Project      : WpfHexEditorControl
// File         : Models/TextDiffResult.cs
// Description  : Result of a line-based text comparison with optional word-level edits.
// Architecture : Pure model — no WPF, no I/O.

namespace WpfHexEditor.Core.Diff.Models;

/// <summary>Classification of a text diff line in a unified or split view.</summary>
public enum TextLineKind
{
    /// <summary>Line exists identically in both files.</summary>
    Equal,
    /// <summary>Line was modified (exists in both but content differs).</summary>
    Modified,
    /// <summary>Line exists only in the left file (deleted in right).</summary>
    DeletedLeft,
    /// <summary>Line exists only in the right file (inserted in right).</summary>
    InsertedRight
}

/// <summary>
/// A single line in a text diff result.
/// In side-by-side mode, <see cref="LeftLineNumber"/> or <see cref="RightLineNumber"/>
/// can be <c>null</c> when the line does not exist on that side.
/// </summary>
public sealed class TextDiffLine
{
    public TextLineKind Kind           { get; init; }
    public string       Content        { get; init; } = string.Empty;
    public int?         LeftLineNumber  { get; init; }
    public int?         RightLineNumber { get; init; }

    /// <summary>
    /// Character-level diff edits within this line.
    /// Only populated for <see cref="TextLineKind.Modified"/> lines
    /// when the line length is ≤ 500 characters.
    /// </summary>
    public IReadOnlyList<DiffEdit> WordEdits { get; init; } = [];

    /// <summary>Paired line on the opposite side (for Modified lines in side-by-side view).</summary>
    public TextDiffLine? CounterpartLine { get; set; }
}

/// <summary>Aggregate statistics for a text comparison.</summary>
public sealed class TextDiffStats
{
    public int    TotalLines     { get; init; }
    public int    EqualLines     { get; init; }
    public int    ModifiedLines  { get; init; }
    public int    DeletedLines   { get; init; }
    public int    InsertedLines  { get; init; }

    public double Similarity => TotalLines == 0 ? 1.0
        : (double)EqualLines / TotalLines;
}

/// <summary>Complete result of a text (line-based) file comparison.</summary>
public sealed class TextDiffResult
{
    public IReadOnlyList<TextDiffLine> Lines      { get; init; } = [];
    public TextDiffStats               Stats      { get; init; } = new();
    public string?                     FallbackReason { get; init; }
}
