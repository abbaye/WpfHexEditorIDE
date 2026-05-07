// ==========================================================
// Project: whfmt.Analysis
// File: DiffResult.cs
// Description: Immutable result model for a semantic format diff.
//   v1.1: HexDiff inline, ChecksumStatus, StructuralDiff added.
// ==========================================================

namespace WhfmtAnalysis;

/// <summary>Result of a semantic field-level comparison between two binary files.</summary>
public sealed class DiffResult
{
    public string FileA            { get; init; } = "";
    public string FileB            { get; init; } = "";
    public long   SizeA            { get; init; }
    public long   SizeB            { get; init; }
    public string FormatName       { get; init; } = "Unknown";
    public string FormatDetectedA  { get; init; } = "Unknown";
    public string FormatDetectedB  { get; init; } = "Unknown";
    public bool   FormatsMatch     { get; init; }
    public List<string> KeyFields    { get; init; } = [];
    public List<string> IgnoreFields { get; init; } = [];
    public string? GroupBy         { get; init; }
    public List<FieldChange> FieldChanges { get; } = [];
    public long   RawByteDelta     { get; init; }
    public bool   IsIdentical      { get; init; }
    public string? Error           { get; init; }

    // Analysis-2: checksum validation
    public List<ChecksumStatus> ChecksumsA { get; } = [];
    public List<ChecksumStatus> ChecksumsB { get; } = [];

    // Analysis-3: structural diff
    public StructuralDiff? StructuralDiff { get; set; }

    public int ChangedCount   => FieldChanges.Count(f => !f.IsIgnored && f.IsChanged);
    public int UnchangedCount => FieldChanges.Count(f => !f.IsIgnored && !f.IsChanged);
    public int CorruptedCountA => ChecksumsA.Count(c => !c.IsValid);
    public int CorruptedCountB => ChecksumsB.Count(c => !c.IsValid);
}

/// <summary>Comparison result for a single named field.</summary>
public sealed class FieldChange
{
    public string FieldName  { get; init; } = "";
    public string ValueA     { get; init; } = "";
    public string ValueB     { get; init; } = "";
    public bool   IsChanged  { get; init; }
    public bool   IsIgnored  { get; init; }
    /// <summary>True when a checksum field is structurally valid in A but corrupt in B (or vice-versa).</summary>
    public bool   IsCorrupted { get; init; }
    /// <summary>Side-by-side hex comparison, populated when both values are byte arrays.</summary>
    public HexDiff? HexDiff  { get; init; }
}

/// <summary>Side-by-side hex comparison for a single field.</summary>
public sealed class HexDiff
{
    /// <summary>Field offset in the file.</summary>
    public long   Offset   { get; init; }
    public byte[] BytesA   { get; init; } = [];
    public byte[] BytesB   { get; init; } = [];
    /// <summary>Per-byte difference mask — true where bytes differ.</summary>
    public bool[] DiffMask { get; init; } = [];
    /// <summary>Number of bytes that differ.</summary>
    public int DifferentBytes => DiffMask.Count(d => d);
}

/// <summary>Checksum validation result for one checksum entry in a file.</summary>
public sealed class ChecksumStatus
{
    public string Algorithm    { get; init; } = "";
    public long   StoredOffset { get; init; }
    public string StoredHex    { get; init; } = "";
    public string ComputedHex  { get; init; } = "";
    public bool   IsValid      { get; init; }
}

/// <summary>Structural comparison of detected blocks between two files.</summary>
public sealed class StructuralDiff
{
    public IReadOnlyList<StructuralBlock> OnlyInA  { get; init; } = [];
    public IReadOnlyList<StructuralBlock> OnlyInB  { get; init; } = [];
    public IReadOnlyList<StructuralBlock> InBoth   { get; init; } = [];
    public int TotalA => OnlyInA.Count + InBoth.Count;
    public int TotalB => OnlyInB.Count + InBoth.Count;
}

/// <summary>A detected structural block (chunk, entry, object) within a file.</summary>
public sealed class StructuralBlock
{
    public string Name   { get; init; } = "";
    public long   Offset { get; init; }
    public int    Length { get; init; }
    /// <summary>Short hash of the block's bytes for identity comparison.</summary>
    public string Hash   { get; init; } = "";
}
