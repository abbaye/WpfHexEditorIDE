// Project      : WpfHexEditorControl
// File         : Models/BinaryByteKind.cs
// Description  : Per-byte classification for the hex dump diff view.
// Architecture : Pure model — no WPF, no I/O.

namespace WpfHexEditor.Core.Diff.Models;

/// <summary>
/// Classification of a single byte position in a side-by-side hex dump diff row.
/// </summary>
public enum BinaryByteKind : byte
{
    /// <summary>Byte is present in both files with the same value.</summary>
    Equal = 0,

    /// <summary>Byte is present in both files but has a different value.</summary>
    Modified = 1,

    /// <summary>Byte exists only in the right file (left cell is a padding slot).</summary>
    InsertedRight = 2,

    /// <summary>Byte exists only in the left file (right cell is a padding slot).</summary>
    DeletedLeft = 3,

    /// <summary>Filler slot used to maintain 16-byte row alignment; no real byte.</summary>
    Padding = 4,
}
