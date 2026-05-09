// ==========================================================
// Project: whfmt.Backfill
// File: Parsing/WhfmtParser.cs
// Description: Parses .whfmt JSON into a WhfmtSummary projection used by inferrers.
// Architecture: System.Text.Json read-only; tolerant to missing fields.
// ==========================================================

using System.Text.Json;
using WhfmtBackfill.Models;

namespace WhfmtBackfill.Parsing;

/// <summary>Reads a .whfmt file and produces a <see cref="WhfmtSummary"/>.</summary>
public static class WhfmtParser
{
    public static WhfmtSummary Parse(string json)
    {
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            CommentHandling     = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        });
        var root = doc.RootElement;

        string formatId   = TryGetString(root, "formatId")   ?? TryGetString(root, "FormatId")   ?? "";
        string category   = TryGetString(root, "category")   ?? TryGetString(root, "Category")   ?? "";
        string formatName = TryGetString(root, "formatName") ?? TryGetString(root, "FormatName") ?? formatId;

        var blocks    = ParseBlocks(root);
        var checksums = ParseChecksums(root);

        return new WhfmtSummary
        {
            FormatId          = formatId,
            Category          = category,
            FormatName        = formatName,
            Blocks            = blocks,
            Checksums         = checksums,
            HasDiffBlock      = root.TryGetProperty("diff",      out _),
            HasRepairBlock    = root.TryGetProperty("repair",    out _),
            HasFuzzBlock      = root.TryGetProperty("fuzz",      out _),
            HasMigrationBlock = root.TryGetProperty("migration", out _),
        };
    }

    private static List<BlockInfo> ParseBlocks(JsonElement root)
    {
        var list = new List<BlockInfo>();
        if (!root.TryGetProperty("blocks", out var blocks) || blocks.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var b in blocks.EnumerateArray())
        {
            string type      = TryGetString(b, "type")      ?? "";
            string name      = TryGetString(b, "name")      ?? "";
            string storeAs   = TryGetString(b, "storeAs")   ?? "";
            string valueType = TryGetString(b, "valueType") ?? "";
            long   offset    = TryGetLong  (b, "offset")    ?? 0;
            int    length    = (int)(TryGetLong(b, "length") ?? 0);

            bool   isSig     = string.Equals(type, "signature", StringComparison.OrdinalIgnoreCase);
            bool   hasMap    = b.TryGetProperty("valueMap",   out var vm) && vm.ValueKind == JsonValueKind.Object && vm.EnumerateObject().Any();
            bool   hasFlags  = b.TryGetProperty("bitfields", out var bf) && bf.ValueKind == JsonValueKind.Array  && bf.EnumerateArray().Any();
            string? expected = null;
            if (b.TryGetProperty("validation", out var val) && val.ValueKind == JsonValueKind.Object)
                expected = TryGetString(val, "expectedValue");

            // Skip non-byte blocks (computed, conditional, metadata) — they don't represent file bytes
            if (type is "computeFromVariables" or "conditional" or "metadata") continue;

            list.Add(new BlockInfo(type, name, storeAs, offset, length, valueType, isSig, hasMap, hasFlags, expected));
        }
        return list;
    }

    private static List<ChecksumInfo> ParseChecksums(JsonElement root)
    {
        var list = new List<ChecksumInfo>();
        if (!root.TryGetProperty("checksums", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var c in arr.EnumerateArray())
        {
            string algo = TryGetString(c, "algorithm") ?? "";
            long  off   = 0;
            int   len   = 0;
            if (c.TryGetProperty("storedAt", out var sa) && sa.ValueKind == JsonValueKind.Object)
            {
                off = TryGetLong(sa, "fixedOffset") ?? 0;
                len = (int)(TryGetLong(sa, "length") ?? 0);
            }
            list.Add(new ChecksumInfo(algo, off, len));
        }
        return list;
    }

    private static string? TryGetString(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static long? TryGetLong(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out long n) ? n : null;
}
