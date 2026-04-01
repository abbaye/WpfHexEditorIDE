// Project      : WpfHexEditorControl
// File         : Models/DiffCompareOptions.cs
// Description  : Options controlling how text lines are compared in the diff algorithm.
// Architecture : Pure model — no WPF, no I/O.

namespace WpfHexEditor.Core.Diff.Models;

/// <summary>
/// Options that control how text lines are compared during the diff algorithm.
/// </summary>
public sealed record DiffCompareOptions
{
    /// <summary>
    /// When <see langword="true"/>, leading/trailing whitespace is ignored when comparing lines.
    /// Lines differing only in whitespace are treated as equal.
    /// </summary>
    public bool IgnoreWhitespace { get; init; }

    /// <summary>Default options — strict ordinal comparison.</summary>
    public static DiffCompareOptions Default { get; } = new();
}
