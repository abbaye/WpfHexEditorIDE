// GNU Affero General Public License v3.0 - 2026
// Contributors: Claude Sonnet 4.6

using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace WpfHexEditor.Editor.Core;

/// <summary>
/// Reads and writes <see cref="ChangesetDto"/> to/from .whchg JSON streams.
/// Uses System.Text.Json with camelCase naming to match the WHChg format spec.
/// </summary>
public static class ChangesetSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented              = true,
        DefaultIgnoreCondition     = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy       = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    // -- I/O ----------------------------------------------------------------

    /// <summary>Serialises a <see cref="ChangesetDto"/> to a stream asynchronously.</summary>
    public static Task WriteAsync(ChangesetDto dto, Stream dest, CancellationToken ct = default)
        => JsonSerializer.SerializeAsync(dest, dto, Options, ct);

    /// <summary>Deserialises a <see cref="ChangesetDto"/> from a stream asynchronously.</summary>
    public static Task<ChangesetDto?> ReadAsync(Stream src, CancellationToken ct = default)
        => JsonSerializer.DeserializeAsync<ChangesetDto>(src, Options, ct).AsTask();

    /// <summary>Deserialises a <see cref="ChangesetDto"/> synchronously.
    /// Safe on the UI thread for small .whchg files.</summary>
    public static ChangesetDto? Read(Stream src)
        => JsonSerializer.Deserialize<ChangesetDto>(src, Options);

    // -- Conversion helpers -------------------------------------------------

    /// <summary>
    /// Converts a <see cref="ChangesetSnapshot"/> to a <see cref="ChangesetDto"/>.
    /// </summary>
    public static ChangesetDto ToDto(
        ChangesetSnapshot snap,
        string?          sourceFile,
        string?          sourceHash,
        DateTimeOffset   created)
    {
        var dto = new ChangesetDto
        {
            SourceFile = sourceFile,
            SourceHash = sourceHash,
            Created    = created,
            Modified   = DateTimeOffset.UtcNow,
        };

        foreach (var m in snap.Modified)
            dto.Edits.Modified.Add(new ModifiedEntryDto
            {
                Offset = FormatOffset(m.Offset),
                Values = FormatBytes(m.Values),
            });

        foreach (var ins in snap.Inserted)
            dto.Edits.Inserted.Add(new InsertedEntryDto
            {
                Offset = FormatOffset(ins.Offset),
                Bytes  = FormatBytes(ins.Bytes),
            });

        foreach (var del in snap.Deleted)
            dto.Edits.Deleted.Add(new DeletedRangeDto
            {
                Start = FormatOffset(del.Start),
                Count = del.Count,
            });

        return dto;
    }

    // -- Parsing helpers ----------------------------------------------------

    /// <summary>Parses a hex or decimal offset string, e.g. "0x0400" or "1024".</summary>
    public static long ParseOffset(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0;
        return s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? Convert.ToInt64(s.Substring(2), 16)
            : long.Parse(s);
    }

    /// <summary>Parses a space-separated hex byte string, e.g. "FF AA BB".</summary>
    public static byte[] ParseHexBytes(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return Array.Empty<byte>();
        var parts = s.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var result = new byte[parts.Length];
        for (int i = 0; i < parts.Length; i++)
            result[i] = Convert.ToByte(parts[i], 16);
        return result;
    }

    // -- Private formatting -------------------------------------------------

    private static string FormatOffset(long offset) => $"0x{offset:X}";

    private static string FormatBytes(byte[] bytes)
        => string.Join(" ", Array.ConvertAll(bytes, b => b.ToString("X2")));
}
