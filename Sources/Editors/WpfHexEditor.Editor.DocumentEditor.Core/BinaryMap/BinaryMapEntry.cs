// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor.Core
// File: BinaryMap/BinaryMapEntry.cs
// Description: Single offset↔block mapping entry in BinaryMap.
// ==========================================================

using WpfHexEditor.Editor.DocumentEditor.Core.Model;

namespace WpfHexEditor.Editor.DocumentEditor.Core.BinaryMap;

/// <summary>
/// Maps a <see cref="DocumentBlock"/> to its byte range in the source file.
/// </summary>
/// <param name="Block">The logical document block.</param>
/// <param name="Offset">Absolute byte offset in the source file.</param>
/// <param name="Length">Length in bytes.</param>
public readonly record struct BinaryMapEntry(DocumentBlock Block, long Offset, int Length)
{
    /// <summary>Exclusive end offset (<see cref="Offset"/> + <see cref="Length"/>).</summary>
    public long End => Offset + Length;

    /// <summary>Returns true if <paramref name="offset"/> falls within this entry's range.</summary>
    public bool Contains(long offset) => offset >= Offset && offset < End;
}
