// Project      : WpfHexEditorControl
// File         : Models/BinaryDiffResult.cs
// Description  : Result of a byte-level binary comparison.
// Architecture : Pure model — no WPF, no I/O.

namespace WpfHexEditor.Core.Diff.Models;

/// <summary>Type of a binary diff region.</summary>
public enum BinaryRegionKind { Identical, Modified, InsertedInRight, DeletedInRight }

/// <summary>A contiguous region of difference in a binary comparison.</summary>
public sealed class BinaryDiffRegion
{
    /// <summary>Byte offset in the left file where this region starts.</summary>
    public long   LeftOffset  { get; init; }

    /// <summary>Byte offset in the right file where this region starts.</summary>
    public long   RightOffset { get; init; }

    /// <summary>Length of this region in bytes (left side for Modified/Deleted; right side for Inserted).</summary>
    public int    Length      { get; init; }

    /// <summary>Actual bytes from the left file (empty for InsertedInRight).</summary>
    public byte[] LeftBytes   { get; init; } = [];

    /// <summary>Actual bytes from the right file (empty for DeletedInRight).</summary>
    public byte[] RightBytes  { get; init; } = [];

    /// <summary>Classification of this region.</summary>
    public BinaryRegionKind Kind { get; init; }
}

/// <summary>Aggregate statistics for a binary comparison.</summary>
public sealed class BinaryDiffStats
{
    public int  TotalRegions     { get; init; }
    public int  ModifiedCount    { get; init; }
    public int  InsertedCount    { get; init; }
    public int  DeletedCount     { get; init; }
    public long ModifiedBytes    { get; init; }
    public long InsertedBytes    { get; init; }
    public long DeletedBytes     { get; init; }
    public long LeftFileSize     { get; init; }
    public long RightFileSize    { get; init; }

    /// <summary>Similarity ratio between 0.0 and 1.0.</summary>
    public double Similarity => LeftFileSize == 0 ? 1.0
        : Math.Max(0.0, 1.0 - (double)(ModifiedBytes + InsertedBytes + DeletedBytes) / Math.Max(LeftFileSize, RightFileSize));
}

/// <summary>Complete result of a binary file comparison.</summary>
public sealed class BinaryDiffResult
{
    public IReadOnlyList<BinaryDiffRegion> Regions    { get; init; } = [];
    public BinaryDiffStats                 Stats      { get; init; } = new();
    public bool                            Truncated  { get; init; }
    public string?                         TruncatedReason { get; init; }
}
