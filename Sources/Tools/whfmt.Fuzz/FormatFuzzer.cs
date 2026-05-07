// ==========================================================
// Project: whfmt.Fuzz
// File: FormatFuzzer.cs
// Description: Public entry point — format-aware binary mutation engine.
//              Uses fuzz strategies declared in .whfmt definitions.
// ==========================================================

using System.Security.Cryptography;
using System.Text.Json;
using WpfHexEditor.Core.Definitions;
using WpfHexEditor.Core.Definitions.Matching;
using WpfHexEditor.Core.Contracts;

namespace WhfmtFuzz;

/// <summary>Generates format-aware mutant files for fuzzing parsers and decoders.</summary>
public static class FormatFuzzer
{
    /// <summary>
    /// Generate <paramref name="count"/> mutant variants of <paramref name="inputFile"/>.
    /// Each variant applies one or more mutations declared in the format's fuzz block.
    /// </summary>
    public static IReadOnlyList<FuzzVariant> Generate(
        IEmbeddedFormatCatalog catalog,
        string inputFile,
        int count = 10,
        string? forcedFormat = null,
        int? seed = null)
    {
        byte[] data = File.ReadAllBytes(inputFile);
        return Generate(catalog, data, Path.GetFileName(inputFile), count, forcedFormat, seed);
    }

    /// <summary>Generate mutant variants from raw byte data.</summary>
    public static IReadOnlyList<FuzzVariant> Generate(
        IEmbeddedFormatCatalog catalog,
        byte[] inputData,
        string fileName,
        int count = 10,
        string? forcedFormat = null,
        int? seed = null)
    {
        var rng = seed.HasValue ? new Random(seed.Value) : new Random();

        var entry = forcedFormat is not null
            ? catalog.GetAll().FirstOrDefault(e => e.Name.Equals(forcedFormat, StringComparison.OrdinalIgnoreCase) || e.Extensions.Any(x => x.TrimStart('.').Equals(forcedFormat.TrimStart('.'), StringComparison.OrdinalIgnoreCase)))
            : FormatFileAnalyzer.Analyze(catalog, new MemoryStream(inputData), Path.GetExtension(fileName))?.Entry;

        if (entry is null)
            return [FuzzVariant.ErrorVariant(fileName, "Could not detect format.")];

        var json = catalog.GetJson(entry.ResourceKey);
        if (json is null)
            return [FuzzVariant.ErrorVariant(fileName, "No full definition for this format.")];

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var strategies = ParseStrategies(root);
        bool preserveChecksums = root.TryGetProperty("fuzz", out var fuzzEl) &&
                                 fuzzEl.TryGetProperty("preserveChecksums", out var pc) &&
                                 pc.GetBoolean();

        var variants = new List<FuzzVariant>(count);
        for (int i = 0; i < count; i++)
        {
            var strategy = strategies.Count > 0
                ? WeightedPick(strategies, rng)
                : new FuzzStrategy { Field = "raw_data", Mutation = MutationType.BitFlip, Rate = 0.001f };

            if (rng.NextDouble() > strategy.Rate) { i--; continue; }

            byte[] mutated = Mutate(inputData, strategy, root, rng);
            if (preserveChecksums) mutated = RecomputeChecksums(mutated, root);

            variants.Add(new FuzzVariant
            {
                Index         = i,
                OriginalFile  = fileName,
                FormatName    = entry.Name,
                Strategy      = strategy.Mutation.ToString(),
                Field         = strategy.Field,
                Description   = strategy.Description,
                Data          = mutated,
                MutationCount = 1,
            });
        }

        return variants;
    }

    // ── Mutation engine ──────────────────────────────────────────────────────

    private static byte[] Mutate(byte[] data, FuzzStrategy strategy, JsonElement root, Random rng)
    {
        byte[] result = (byte[])data.Clone();
        var (offset, length) = ResolveField(strategy.Field, root, result);

        if (offset < 0 || length <= 0 || offset + length > result.Length)
        {
            // Fallback: random position in file
            offset = rng.Next(0, Math.Max(1, result.Length - 4));
            length = Math.Min(4, result.Length - (int)offset);
        }

        switch (strategy.Mutation)
        {
            case MutationType.BoundaryValues:
                ApplyBoundaryValue(result, (int)offset, length, rng);
                break;

            case MutationType.EnumSweep:
                var enumVals = ParseValueMap(strategy.Field, root);
                if (enumVals.Count > 0)
                {
                    var picked = rng.Next(0, enumVals.Count + 5); // +5 invalid values
                    byte val = picked < enumVals.Count ? (byte)enumVals[picked] : (byte)rng.Next(200, 256);
                    if (offset < result.Length) result[offset] = val;
                }
                break;

            case MutationType.CorruptSignature:
                for (int i = 0; i < Math.Min(length, result.Length - (int)offset); i++)
                    result[(int)offset + i] ^= (byte)(rng.Next(1, 255));
                break;

            case MutationType.BitFlip:
                int byteIdx = (int)offset + rng.Next(0, length);
                if (byteIdx < result.Length)
                    result[byteIdx] ^= (byte)(1 << rng.Next(0, 8));
                break;

            case MutationType.ZeroField:
                Array.Clear(result, (int)offset, Math.Min(length, result.Length - (int)offset));
                break;

            case MutationType.Overflow:
                for (int i = (int)offset; i < Math.Min((int)offset + length, result.Length); i++)
                    result[i] = 0xFF;
                break;

            case MutationType.RandomBytes:
                rng.NextBytes(result.AsSpan((int)offset, Math.Min(length, result.Length - (int)offset)));
                break;

            case MutationType.Truncate:
                int truncAt = (int)offset + length / 2;
                if (truncAt > 0 && truncAt < result.Length)
                    result = result[..truncAt];
                break;

            case MutationType.Duplicate:
                int srcEnd = (int)offset + length;
                if (srcEnd <= result.Length)
                {
                    byte[] segment = result[(int)offset..srcEnd];
                    var grown = new byte[result.Length + segment.Length];
                    Array.Copy(result, grown, (int)offset + length);
                    Array.Copy(segment, 0, grown, (int)offset + length, segment.Length);
                    Array.Copy(result, (int)offset + length, grown, (int)offset + length + segment.Length, result.Length - (int)offset - length);
                    result = grown;
                }
                break;
        }

        return result;
    }

    private static void ApplyBoundaryValue(byte[] data, int offset, int length, Random rng)
    {
        long[] boundaries = [0, 1, 127, 128, 255, 256, 32767, 32768, 65535, 65536, int.MaxValue, (long)uint.MaxValue];
        long chosen = boundaries[rng.Next(boundaries.Length)];
        WriteInt(data, offset, length, chosen);
    }

    private static void WriteInt(byte[] data, int offset, int length, long value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        int copy = Math.Min(length, Math.Min(bytes.Length, data.Length - offset));
        Array.Copy(bytes, 0, data, offset, copy);
    }

    private static (long offset, int length) ResolveField(string fieldName, JsonElement root, byte[] data)
    {
        if (!root.TryGetProperty("blocks", out var blocks)) return (-1, 0);
        foreach (var block in blocks.EnumerateArray())
        {
            string? name = block.TryGetProperty("name",    out var n) ? n.GetString() : null;
            string? store= block.TryGetProperty("storeAs", out var s) ? s.GetString() : null;
            if (!string.Equals(name, fieldName, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(store, fieldName, StringComparison.OrdinalIgnoreCase))
                continue;

            long off = block.TryGetProperty("offset", out var ov) && ov.ValueKind == JsonValueKind.Number ? ov.GetInt64() : 0;
            int  len = block.TryGetProperty("length", out var lv) && lv.ValueKind == JsonValueKind.Number ? lv.GetInt32() : 1;
            return (off, len);
        }
        return (-1, 0);
    }

    private static List<int> ParseValueMap(string fieldName, JsonElement root)
    {
        if (!root.TryGetProperty("blocks", out var blocks)) return [];
        foreach (var block in blocks.EnumerateArray())
        {
            string? name = block.TryGetProperty("name", out var n) ? n.GetString() : null;
            if (!string.Equals(name, fieldName, StringComparison.OrdinalIgnoreCase)) continue;
            if (!block.TryGetProperty("valueMap", out var vm)) return [];
            var result = new List<int>();
            foreach (var kv in vm.EnumerateObject())
                if (int.TryParse(kv.Name, out int v)) result.Add(v);
            return result;
        }
        return [];
    }

    private static byte[] RecomputeChecksums(byte[] data, JsonElement root)
    {
        if (!root.TryGetProperty("checksums", out var checksums)) return data;
        foreach (var cs in checksums.EnumerateArray())
        {
            string algo = cs.TryGetProperty("algorithm", out var av) ? av.GetString() ?? "" : "";
            if (!cs.TryGetProperty("storedAt", out var sat)) continue;
            long storedOffset = sat.TryGetProperty("fixedOffset", out var sfo) ? sfo.GetInt64() : -1;
            int  storedLen    = sat.TryGetProperty("length",      out var sl)  ? sl.GetInt32()  : 4;
            if (storedOffset < 0 || storedOffset + storedLen > data.Length) continue;

            long dataOffset = cs.TryGetProperty("dataRange", out var dr) && dr.TryGetProperty("fixedOffset", out var dfo) ? dfo.GetInt64() : 0;
            long dataLength = cs.TryGetProperty("dataRange", out var dr2) && dr2.TryGetProperty("fixedLength", out var dfl) ? dfl.GetInt64() : data.Length - dataOffset;
            if (dataOffset < 0 || dataLength <= 0 || dataOffset + dataLength > data.Length) continue;

            byte[] slice = data[(int)dataOffset..(int)(dataOffset + dataLength)];
            byte[]? computed = ComputeChecksum(slice, algo);
            if (computed is null) continue;

            int copy = Math.Min(storedLen, Math.Min(computed.Length, data.Length - (int)storedOffset));
            Array.Copy(computed, 0, data, storedOffset, copy);
        }
        return data;
    }

    private static byte[]? ComputeChecksum(byte[] data, string algorithm) => algorithm.ToLowerInvariant() switch
    {
        "crc32"  => BitConverter.GetBytes(Crc32(data)),
        "md5"    => MD5.HashData(data),
        "sha1"   => SHA1.HashData(data),
        "sha256" => SHA256.HashData(data),
        _        => null
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

    // ── Strategy parsing ─────────────────────────────────────────────────────

    private static List<FuzzStrategy> ParseStrategies(JsonElement root)
    {
        var list = new List<FuzzStrategy>();
        if (!root.TryGetProperty("fuzz", out var fuzz)) return list;
        if (!fuzz.TryGetProperty("strategies", out var arr)) return list;

        foreach (var s in arr.EnumerateArray())
        {
            string field    = s.TryGetProperty("field",       out var f) ? f.GetString() ?? "" : "";
            string mutation = s.TryGetProperty("mutation",    out var m) ? m.GetString() ?? "" : "";
            float  rate     = s.TryGetProperty("rate",        out var r) ? (float)r.GetDouble() : 1.0f;
            float  weight   = s.TryGetProperty("weight",      out var w) ? (float)w.GetDouble() : 1.0f;
            string desc     = s.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";

            if (!Enum.TryParse<MutationType>(ToPascal(mutation), out var mt)) mt = MutationType.RandomBytes;
            list.Add(new FuzzStrategy { Field = field, Mutation = mt, Rate = rate, Weight = weight, Description = desc });
        }
        return list;
    }

    private static FuzzStrategy WeightedPick(List<FuzzStrategy> strategies, Random rng)
    {
        float total = strategies.Sum(s => s.Weight);
        float pick  = (float)(rng.NextDouble() * total);
        float acc   = 0;
        foreach (var s in strategies) { acc += s.Weight; if (pick <= acc) return s; }
        return strategies[^1];
    }

    private static string ToPascal(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return string.Concat(s.Split('_').Select(p => char.ToUpper(p[0]) + p[1..]));
    }
}

internal sealed class FuzzStrategy
{
    public string       Field       { get; init; } = "";
    public MutationType Mutation    { get; init; }
    public float        Rate        { get; init; } = 1.0f;
    public float        Weight      { get; init; } = 1.0f;
    public string       Description { get; init; } = "";
}
