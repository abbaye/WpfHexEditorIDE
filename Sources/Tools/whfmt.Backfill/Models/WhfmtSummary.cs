// ==========================================================
// Project: whfmt.Backfill
// File: Models/WhfmtSummary.cs
// Description: Lightweight projection of a .whfmt file's relevant fields for inference.
// Architecture: Read-only DTO populated by WhfmtParser; consumed by inferrers.
// ==========================================================

namespace WhfmtBackfill.Models;

/// <summary>Block descriptor extracted from a .whfmt file's "blocks" array.</summary>
public sealed record BlockInfo(
    string Type,
    string Name,
    string StoreAs,
    long   Offset,
    int    Length,
    string ValueType,
    bool   IsSignature,
    bool   HasValueMap,
    bool   HasBitfields,
    string? ExpectedValue);

/// <summary>Checksum descriptor extracted from a .whfmt file's "checksums" array.</summary>
public sealed record ChecksumInfo(
    string Algorithm,
    long   StoredOffset,
    int    StoredLength);

/// <summary>Compact view of a .whfmt file used by all inferrers. Pure data, no JSON nodes.</summary>
public sealed class WhfmtSummary
{
    public required string FormatId       { get; init; }
    public required string Category       { get; init; }
    public required string FormatName     { get; init; }
    public IReadOnlyList<BlockInfo>    Blocks    { get; init; } = [];
    public IReadOnlyList<ChecksumInfo> Checksums { get; init; } = [];

    public bool HasDiffBlock      { get; init; }
    public bool HasRepairBlock    { get; init; }
    public bool HasFuzzBlock      { get; init; }
    public bool HasMigrationBlock { get; init; }
}
