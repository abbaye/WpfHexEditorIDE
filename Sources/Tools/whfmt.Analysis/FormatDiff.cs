// ==========================================================
// Project: whfmt.Analysis
// File: FormatDiff.cs
// Description: Field-level semantic diff between two binary files using whfmt definitions.
// Architecture: v1.1 — HexDiff inline, checksum validation, structural diff, async overloads.
// ==========================================================

using System.Security.Cryptography;
using System.Text.Json;
using WpfHexEditor.Core.Definitions;
using WpfHexEditor.Core.Definitions.Matching;
using WpfHexEditor.Core.Contracts;

namespace WhfmtAnalysis;

/// <summary>Compares two binary files at the field level using whfmt format definitions.</summary>
public static class FormatDiff
{
    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>Compare two files semantically using their shared whfmt format definition.</summary>
    public static DiffResult Compare(
        IEmbeddedFormatCatalog catalog,
        string fileA,
        string fileB,
        string? forcedFormat = null)
    {
        var dataA = File.ReadAllBytes(fileA);
        var dataB = File.ReadAllBytes(fileB);
        return Compare(catalog, dataA, fileA, dataB, fileB, forcedFormat);
    }

    /// <summary>Compare two files from raw byte arrays.</summary>
    public static DiffResult Compare(
        IEmbeddedFormatCatalog catalog,
        byte[] dataA, string nameA,
        byte[] dataB, string nameB,
        string? forcedFormat = null)
    {
        var matchA = forcedFormat is not null
            ? MatchForced(catalog, forcedFormat)
            : FormatFileAnalyzer.Analyze(catalog, new MemoryStream(dataA), Path.GetExtension(nameA));

        var matchB = forcedFormat is not null
            ? MatchForced(catalog, forcedFormat)
            : FormatFileAnalyzer.Analyze(catalog, new MemoryStream(dataB), Path.GetExtension(nameB));

        var result = new DiffResult
        {
            FileA            = nameA,
            FileB            = nameB,
            SizeA            = dataA.Length,
            SizeB            = dataB.Length,
            FormatName       = matchA?.Entry.Name ?? matchB?.Entry.Name ?? "Unknown",
            FormatDetectedA  = matchA?.Entry.Name ?? "Unknown",
            FormatDetectedB  = matchB?.Entry.Name ?? "Unknown",
            FormatsMatch     = matchA?.Entry.Name == matchB?.Entry.Name,
        };

        var entry = matchA?.Entry ?? matchB?.Entry;
        if (entry is null) { result.Error = "Could not detect format for either file."; return result; }

        var json = catalog.GetJson(entry.ResourceKey);
        if (json is null) { result.Error = "No full definition available for this format."; return result; }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var diffConfig  = ParseDiffConfig(root);
        result.KeyFields    = diffConfig.KeyFields;
        result.IgnoreFields = diffConfig.IgnoreFields;
        result.GroupBy      = diffConfig.GroupBy;

        var varsA = ExtractVariables(root, dataA);
        var varsB = ExtractVariables(root, dataB);
        var rawA  = ExtractRawFields(root, dataA);
        var rawB  = ExtractRawFields(root, dataB);

        // Analysis-1: field diff with HexDiff inline
        foreach (var field in diffConfig.KeyFields)
        {
            varsA.TryGetValue(field, out var valA);
            varsB.TryGetValue(field, out var valB);
            string strA = valA?.ToString() ?? "(not found)";
            string strB = valB?.ToString() ?? "(not found)";
            bool equal  = string.Equals(strA, strB, StringComparison.OrdinalIgnoreCase);

            HexDiff? hexDiff = null;
            if (rawA.TryGetValue(field, out var rawBytesA) && rawB.TryGetValue(field, out var rawBytesB))
                hexDiff = BuildHexDiff(field, rawBytesA, rawBytesB, root);

            result.FieldChanges.Add(new FieldChange
            {
                FieldName = field,
                ValueA    = strA,
                ValueB    = strB,
                IsChanged = !equal,
                IsIgnored = false,
                HexDiff   = hexDiff,
            });
        }

        foreach (var field in diffConfig.IgnoreFields)
        {
            varsA.TryGetValue(field, out var valA);
            varsB.TryGetValue(field, out var valB);
            result.FieldChanges.Add(new FieldChange
            {
                FieldName = field,
                ValueA    = valA?.ToString() ?? "(not found)",
                ValueB    = valB?.ToString() ?? "(not found)",
                IsChanged = false,
                IsIgnored = true,
            });
        }

        // Analysis-2: checksum validation
        FillChecksumStatus(root, dataA, result.ChecksumsA);
        FillChecksumStatus(root, dataB, result.ChecksumsB);

        // Mark corrupted fields (valid in A, invalid in B)
        foreach (var change in result.FieldChanges)
        {
            var csA = result.ChecksumsA.FirstOrDefault(c => c.Algorithm.Contains(change.FieldName, StringComparison.OrdinalIgnoreCase));
            var csB = result.ChecksumsB.FirstOrDefault(c => c.Algorithm.Contains(change.FieldName, StringComparison.OrdinalIgnoreCase));
            if (csA is not null && csB is not null && csA.IsValid && !csB.IsValid)
            {
                // Replace the FieldChange with IsCorrupted=true (rebuild since sealed class)
                int idx = result.FieldChanges.IndexOf(change);
                result.FieldChanges[idx] = new FieldChange
                {
                    FieldName   = change.FieldName,
                    ValueA      = change.ValueA,
                    ValueB      = change.ValueB,
                    IsChanged   = change.IsChanged,
                    IsIgnored   = change.IsIgnored,
                    IsCorrupted = true,
                    HexDiff     = change.HexDiff,
                };
            }
        }

        // Analysis-3: structural diff
        result.StructuralDiff = BuildStructuralDiff(root, dataA, dataB);

        result.RawByteDelta = dataB.Length - dataA.Length;
        result.IsIdentical  = result.FieldChanges.All(c => c.IsIgnored || !c.IsChanged)
                           && dataA.Length == dataB.Length;

        return result;
    }

    /// <summary>Async overload — reads files asynchronously then compares.</summary>
    public static async Task<DiffResult> CompareAsync(
        IEmbeddedFormatCatalog catalog,
        string fileA,
        string fileB,
        string? forcedFormat = null,
        CancellationToken cancellationToken = default)
    {
        var dataA = await File.ReadAllBytesAsync(fileA, cancellationToken);
        var dataB = await File.ReadAllBytesAsync(fileB, cancellationToken);
        return Compare(catalog, dataA, fileA, dataB, fileB, forcedFormat);
    }

    /// <summary>Async overload from streams — reads all bytes then compares.</summary>
    public static async Task<DiffResult> CompareAsync(
        IEmbeddedFormatCatalog catalog,
        Stream streamA, string nameA,
        Stream streamB, string nameB,
        string? forcedFormat = null,
        CancellationToken cancellationToken = default)
    {
        using var msA = new MemoryStream();
        using var msB = new MemoryStream();
        await streamA.CopyToAsync(msA, cancellationToken);
        await streamB.CopyToAsync(msB, cancellationToken);
        return Compare(catalog, msA.ToArray(), nameA, msB.ToArray(), nameB, forcedFormat);
    }

    // ── Analysis-1: HexDiff ──────────────────────────────────────────────────

    private static HexDiff BuildHexDiff(string fieldName, byte[] a, byte[] b, JsonElement root)
    {
        long offset = 0;
        if (root.TryGetProperty("blocks", out var blocks))
            foreach (var block in blocks.EnumerateArray())
            {
                string? name  = block.TryGetProperty("name",    out var n) ? n.GetString() : null;
                string? store = block.TryGetProperty("storeAs", out var s) ? s.GetString() : null;
                if (!string.Equals(name, fieldName, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(store, fieldName, StringComparison.OrdinalIgnoreCase)) continue;
                offset = block.TryGetProperty("offset", out var ov) && ov.ValueKind == JsonValueKind.Number ? ov.GetInt64() : 0;
                break;
            }

        int maxLen  = Math.Max(a.Length, b.Length);
        var mask    = new bool[maxLen];
        for (int i = 0; i < maxLen; i++)
        {
            byte ba = i < a.Length ? a[i] : (byte)0;
            byte bb = i < b.Length ? b[i] : (byte)0;
            mask[i] = ba != bb;
        }
        return new HexDiff { Offset = offset, BytesA = a, BytesB = b, DiffMask = mask };
    }

    // ── Analysis-2: Checksum validation ─────────────────────────────────────

    private static void FillChecksumStatus(JsonElement root, byte[] data, List<ChecksumStatus> target)
    {
        if (!root.TryGetProperty("checksums", out var checksums)) return;
        foreach (var cs in checksums.EnumerateArray())
        {
            string algo    = cs.TryGetProperty("algorithm", out var av) ? av.GetString() ?? "" : "";
            if (!cs.TryGetProperty("storedAt", out var sat)) continue;
            long storedOff = sat.TryGetProperty("fixedOffset", out var sfo) ? sfo.GetInt64() : -1;
            int  storedLen = sat.TryGetProperty("length",      out var sl)  ? sl.GetInt32()  : 4;
            if (storedOff < 0 || storedOff + storedLen > data.Length) continue;

            long dataOff = cs.TryGetProperty("dataRange", out var dr)  && dr.TryGetProperty("fixedOffset",  out var dfo) ? dfo.GetInt64() : 0;
            long dataLen = cs.TryGetProperty("dataRange", out var dr2) && dr2.TryGetProperty("fixedLength", out var dfl) ? dfl.GetInt64() : data.Length - dataOff;
            if (dataOff < 0 || dataLen <= 0 || dataOff + dataLen > data.Length) continue;

            byte[] stored   = data[(int)storedOff..(int)(storedOff + storedLen)];
            byte[] slice    = data[(int)dataOff..(int)(dataOff + dataLen)];
            byte[]? computed = ComputeChecksum(slice, algo);
            if (computed is null) continue;

            int cmp = Math.Min(stored.Length, computed.Length);
            bool valid = stored.AsSpan(0, cmp).SequenceEqual(computed.AsSpan(0, cmp));

            target.Add(new ChecksumStatus
            {
                Algorithm    = algo.ToUpper(),
                StoredOffset = storedOff,
                StoredHex    = BitConverter.ToString(stored[..cmp]).Replace("-", ""),
                ComputedHex  = BitConverter.ToString(computed[..cmp]).Replace("-", ""),
                IsValid      = valid,
            });
        }
    }

    // ── Analysis-3: Structural diff ──────────────────────────────────────────

    private static StructuralDiff BuildStructuralDiff(JsonElement root, byte[] dataA, byte[] dataB)
    {
        var blocksA = DetectBlocks(root, dataA);
        var blocksB = DetectBlocks(root, dataB);

        var hashesB = blocksB.ToDictionary(b => b.Hash, b => b);
        var hashesA = blocksA.ToDictionary(b => b.Hash, b => b);

        var onlyA  = blocksA.Where(b => !hashesB.ContainsKey(b.Hash)).ToList();
        var onlyB  = blocksB.Where(b => !hashesA.ContainsKey(b.Hash)).ToList();
        var inBoth = blocksA.Where(b =>  hashesB.ContainsKey(b.Hash)).ToList();

        return new StructuralDiff { OnlyInA = onlyA, OnlyInB = onlyB, InBoth = inBoth };
    }

    private static List<StructuralBlock> DetectBlocks(JsonElement root, byte[] data)
    {
        var result = new List<StructuralBlock>();
        if (!root.TryGetProperty("blocks", out var blocks)) return result;
        foreach (var b in blocks.EnumerateArray())
        {
            string name   = b.TryGetProperty("name",    out var n) ? n.GetString() ?? "" : "";
            string storeAs= b.TryGetProperty("storeAs", out var s) ? s.GetString() ?? "" : "";
            long   offset = b.TryGetProperty("offset",  out var o) && o.ValueKind == JsonValueKind.Number ? o.GetInt64() : 0;
            int    length = b.TryGetProperty("length",  out var l) && l.ValueKind == JsonValueKind.Number ? l.GetInt32() : 0;
            if (length <= 0 || offset + length > data.Length) continue;

            byte[] slice = data[(int)offset..(int)(offset + length)];
            string hash  = BitConverter.ToString(MD5.HashData(slice)[..4]).Replace("-", "");
            result.Add(new StructuralBlock
            {
                Name   = string.IsNullOrEmpty(storeAs) ? name : storeAs,
                Offset = offset,
                Length = length,
                Hash   = hash,
            });
        }
        return result;
    }

    // ── Field extraction ─────────────────────────────────────────────────────

    private static (List<string> KeyFields, List<string> IgnoreFields, string? GroupBy) ParseDiffConfig(JsonElement root)
    {
        var key    = new List<string>();
        var ignore = new List<string>();
        string? groupBy = null;
        if (root.TryGetProperty("diff", out var diff))
        {
            if (diff.TryGetProperty("keyFields",    out var kf)) key.AddRange(kf.EnumerateArray().Select(e => e.GetString() ?? "").Where(s => s.Length > 0));
            if (diff.TryGetProperty("ignoreFields", out var ig)) ignore.AddRange(ig.EnumerateArray().Select(e => e.GetString() ?? "").Where(s => s.Length > 0));
            if (diff.TryGetProperty("groupBy",      out var gb)) groupBy = gb.GetString();
        }
        if (key.Count == 0 && root.TryGetProperty("variables", out var vars))
            foreach (var v in vars.EnumerateObject()) key.Add(v.Name);
        return (key, ignore, groupBy);
    }

    private static Dictionary<string, object> ExtractVariables(JsonElement root, byte[] data)
    {
        var vars = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty("variables", out var varBlock))
            foreach (var v in varBlock.EnumerateObject())
                vars[v.Name] = v.Value.ValueKind == JsonValueKind.Number ? (object)v.Value.GetInt64() : (v.Value.GetString() ?? "");
        if (root.TryGetProperty("blocks", out var blocks))
            foreach (var block in blocks.EnumerateArray())
            {
                if (!block.TryGetProperty("storeAs", out var sa)) continue;
                string varName = sa.GetString() ?? "";
                if (string.IsNullOrWhiteSpace(varName)) continue;
                long offset = block.TryGetProperty("offset", out var off) && off.ValueKind == JsonValueKind.Number ? off.GetInt64() : 0;
                int  length = block.TryGetProperty("length", out var len) && len.ValueKind == JsonValueKind.Number ? len.GetInt32() : 0;
                string vt   = block.TryGetProperty("valueType", out var vtEl) ? vtEl.GetString() ?? "" : "";
                if (offset < 0 || length <= 0 || offset + length > data.Length) continue;
                byte[] slice = data[(int)offset..(int)(offset + length)];
                vars[varName] = ReadValue(slice, vt);
            }
        return vars;
    }

    private static Dictionary<string, byte[]> ExtractRawFields(JsonElement root, byte[] data)
    {
        var raw = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty("blocks", out var blocks)) return raw;
        foreach (var block in blocks.EnumerateArray())
        {
            string key    = block.TryGetProperty("storeAs", out var sa) ? sa.GetString() ?? ""
                          : block.TryGetProperty("name",    out var n)  ? n.GetString()  ?? "" : "";
            if (string.IsNullOrEmpty(key)) continue;
            long offset = block.TryGetProperty("offset", out var o) && o.ValueKind == JsonValueKind.Number ? o.GetInt64() : 0;
            int  length = block.TryGetProperty("length", out var l) && l.ValueKind == JsonValueKind.Number ? l.GetInt32() : 0;
            if (length <= 0 || offset + length > data.Length) continue;
            raw[key] = data[(int)offset..(int)(offset + length)];
        }
        return raw;
    }

    private static object ReadValue(byte[] bytes, string valueType) => valueType.ToLowerInvariant() switch
    {
        "uint8"  => (long)bytes[0],
        "uint16" => (long)BitConverter.ToUInt16(bytes, 0),
        "uint32" => (long)BitConverter.ToUInt32(bytes, 0),
        "uint64" => (long)BitConverter.ToUInt64(bytes, 0),
        "int8"   => (long)(sbyte)bytes[0],
        "int16"  => (long)BitConverter.ToInt16(bytes, 0),
        "int32"  => (long)BitConverter.ToInt32(bytes, 0),
        "ascii8" or "utf8" or "string" => System.Text.Encoding.UTF8.GetString(bytes).TrimEnd('\0'),
        _        => BitConverter.ToString(bytes).Replace("-", ""),
    };

    // ── Checksum computation ──────────────────────────────────────────────────

    private static byte[]? ComputeChecksum(byte[] data, string algorithm) => algorithm.ToLowerInvariant() switch
    {
        "crc32"  => BitConverter.GetBytes(Crc32(data)),
        "md5"    => MD5.HashData(data),
        "sha1"   => SHA1.HashData(data),
        "sha256" => SHA256.HashData(data),
        _        => null,
    };

    private static uint Crc32(byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (var b in data) crc = (crc >> 8) ^ _crcTable[(crc & 0xFF) ^ b];
        return ~crc;
    }
    private static readonly uint[] _crcTable = BuildCrcTable();
    private static uint[] BuildCrcTable()
    {
        var t = new uint[256];
        for (uint i = 0; i < 256; i++) { uint c = i; for (int j = 8; j > 0; j--) c = (c & 1) != 0 ? (c >> 1) ^ 0xEDB88320 : c >> 1; t[i] = c; }
        return t;
    }

    private static FormatMatchResult? MatchForced(IEmbeddedFormatCatalog catalog, string format)
    {
        var entry = catalog.GetAll().FirstOrDefault(e =>
            e.Name.Equals(format, StringComparison.OrdinalIgnoreCase) ||
            e.Extensions.Any(x => x.TrimStart('.').Equals(format.TrimStart('.'), StringComparison.OrdinalIgnoreCase)));
        return entry is null ? null : new FormatMatchResult(entry, 1.0, MatchSource.Extension, 1.0);
    }
}
