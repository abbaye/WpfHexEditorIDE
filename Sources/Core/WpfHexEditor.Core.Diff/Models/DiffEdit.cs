// Project      : WpfHexEditorControl
// File         : Models/DiffEdit.cs
// Description  : Atomic edit operation produced by Myers diff algorithm.
// Architecture : Pure immutable record — no WPF, no I/O.

namespace WpfHexEditor.Core.Diff.Models;

/// <summary>The type of a single diff edit operation.</summary>
public enum EditKind { Equal, Insert, Delete }

/// <summary>
/// A single edit produced by <see cref="Algorithms.MyersDiffAlgorithm"/>.
/// Indices are 0-based offsets into the left (A) or right (B) sequence.
/// </summary>
/// <param name="Kind">Operation type.</param>
/// <param name="LeftStart">Inclusive start index in the left sequence (meaningful for Equal/Delete).</param>
/// <param name="LeftEnd">Exclusive end index in the left sequence.</param>
/// <param name="RightStart">Inclusive start index in the right sequence (meaningful for Equal/Insert).</param>
/// <param name="RightEnd">Exclusive end index in the right sequence.</param>
public sealed record DiffEdit(
    EditKind Kind,
    int LeftStart,
    int LeftEnd,
    int RightStart,
    int RightEnd)
{
    /// <summary>Number of elements affected on the left side.</summary>
    public int LeftLength  => LeftEnd  - LeftStart;

    /// <summary>Number of elements affected on the right side.</summary>
    public int RightLength => RightEnd - RightStart;
}
