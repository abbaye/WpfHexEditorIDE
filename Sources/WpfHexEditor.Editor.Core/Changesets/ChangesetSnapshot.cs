// Apache 2.0 - 2026
// Contributors: Claude Sonnet 4.6

namespace WpfHexEditor.Editor.Core;

/// <summary>A contiguous run of consecutive modified bytes at a physical offset.</summary>
public sealed record ModifiedRange(long Offset, byte[] Values);

/// <summary>A block of bytes inserted before a physical offset.</summary>
public sealed record InsertedBlock(long Offset, byte[] Bytes);

/// <summary>A contiguous range of deleted bytes at a physical offset.</summary>
public sealed record DeletedRange(long Start, long Count);

/// <summary>
/// Immutable snapshot of all pending edits in a ByteProvider.
/// Capturing this is O(e) — only iterates the edit dictionaries, never the full file.
/// Used for serialising to a .whchg file without copying the entire file.
/// </summary>
public sealed record ChangesetSnapshot(
    IReadOnlyList<ModifiedRange> Modified,
    IReadOnlyList<InsertedBlock> Inserted,
    IReadOnlyList<DeletedRange>  Deleted)
{
    /// <summary>An empty snapshot with no edits.</summary>
    public static readonly ChangesetSnapshot Empty = new(
        Array.Empty<ModifiedRange>(),
        Array.Empty<InsertedBlock>(),
        Array.Empty<DeletedRange>());

    /// <summary>True when at least one edit is present.</summary>
    public bool HasEdits => Modified.Count > 0 || Inserted.Count > 0 || Deleted.Count > 0;
}
