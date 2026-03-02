// Apache 2.0 - 2026
// Contributors: Claude Sonnet 4.6

namespace WpfHexEditor.Editor.Core;

/// <summary>
/// Root DTO for a .whchg file — JSON format tracking byte-level edits
/// (modify / insert / delete) without touching the physical file.
/// </summary>
public sealed class ChangesetDto
{
    /// <summary>Format version — currently 1.</summary>
    public int Version { get; set; } = 1;

    /// <summary>Base filename of the source file, e.g. "game.smc".</summary>
    public string? SourceFile { get; set; }

    /// <summary>CRC32 of the original source file, e.g. "crc32:1A2B3C4D".</summary>
    public string? SourceHash { get; set; }

    public DateTimeOffset Created  { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset Modified { get; set; } = DateTimeOffset.UtcNow;

    public ChangesetEditsDto Edits { get; set; } = new();
}

/// <summary>The three edit collections stored in a .whchg file.</summary>
public sealed class ChangesetEditsDto
{
    /// <summary>Overwrite edits: consecutive modified bytes grouped into runs.</summary>
    public List<ModifiedEntryDto> Modified { get; set; } = new();

    /// <summary>Insertion edits: bytes to insert before a given offset.</summary>
    public List<InsertedEntryDto> Inserted { get; set; } = new();

    /// <summary>Deletion edits: contiguous ranges of deleted bytes.</summary>
    public List<DeletedRangeDto>  Deleted  { get; set; } = new();
}

/// <summary>{ "offset": "0x0064", "values": "FF AA BB" }</summary>
public sealed class ModifiedEntryDto
{
    /// <summary>Physical offset (hex string, e.g. "0x0064").</summary>
    public string Offset { get; set; } = "0x0000";

    /// <summary>Space-separated hex bytes for the run, e.g. "FF AA BB".</summary>
    public string Values { get; set; } = "";
}

/// <summary>{ "offset": "0x0400", "bytes": "01 02 03 04" }</summary>
public sealed class InsertedEntryDto
{
    /// <summary>Physical offset before which bytes are inserted (hex string).</summary>
    public string Offset { get; set; } = "0x0000";

    /// <summary>Space-separated hex bytes, e.g. "01 02 03 04".</summary>
    public string Bytes { get; set; } = "";
}

/// <summary>{ "start": "0x0800", "count": 3 }</summary>
public sealed class DeletedRangeDto
{
    /// <summary>Physical start offset of the deleted range (hex string).</summary>
    public string Start { get; set; } = "0x0000";

    /// <summary>Number of consecutive deleted bytes.</summary>
    public long Count { get; set; }
}
