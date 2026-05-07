// ==========================================================
// Project: whfmt.Fuzz
// File: FormatFuzzer.cs
// Description: Public entry point — format-aware binary mutation engine.
// Architecture: Uses fuzz strategies declared in .whfmt definitions.
//   v1.1: compound mutations, 3 new strategies (InsertBytes/SliceRepeat/NegateField),
//         async overloads, FuzzReport, FuzzSession.
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
    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>Generate <paramref name="count"/> mutant variants from a file path.</summary>
    public static IReadOnlyList<FuzzVariant> Generate(
        IEmbeddedFormatCatalog catalog,
        string inputFile,
        int count = 10,
        string? forcedFormat = null,
        int? seed = null,
        int compound = 1)
    {
        byte[] data = File.ReadAllBytes(inputFile);
        return Generate(catalog, data, Path.GetFileName(inputFile), count, forcedFormat, seed, compound);
    }

    /// <summary>Generate mutant variants from raw byte data.</summary>
    public static IReadOnlyList<FuzzVariant> Generate(
        IEmbeddedFormatCatalog catalog,
        byte[] inputData,
        string fileName,
        int count = 10,
        string? forcedFormat = null,
        int? seed = null,
        int compound = 1)
    {
        var (entry, json, error) = Resolve(catalog, inputData, fileName, forcedFormat);
        if (error is not null) return [FuzzVariant.ErrorVariant(fileName, error)];
        return GenerateFromResolved(entry!, json!, inputData, fileName, count, seed, compound);
    }

    private static List<FuzzVariant> GenerateFromResolved(
        EmbeddedFormatEntry entry, string json,
        byte[] inputData, string fileName,
        int count, int? seed, int compound)
    {
        using var doc = JsonDocument.Parse(json);
        var root      = doc.RootElement;
        var strategies = ParseStrategies(root);
        bool preserveChecksums = root.TryGetProperty("fuzz", out var fuzzEl) &&
                                 fuzzEl.TryGetProperty("preserveChecksums", out var pc) &&
                                 pc.GetBoolean();

        var rng      = seed.HasValue ? new Random(seed.Value) : Random.Shared;
        var variants = new List<FuzzVariant>(count);
        int attempts = 0;
        int effective = Math.Max(1, compound);

        while (variants.Count < count && attempts < count * 20)
        {
            attempts++;
            var log     = new List<MutationLogEntry>(effective);
            byte[] mutated = (byte[])inputData.Clone();
            var usedFields = new HashSet<string>();

            for (int m = 0; m < effective; m++)
            {
                var strategy = strategies.Count > 0
                    ? WeightedPick(strategies, rng)
                    : new FuzzStrategy { Field = "raw_data", Mutation = MutationType.BitFlip, Rate = 0.001f };

                if (effective == 1 && rng.NextDouble() > strategy.Rate) continue;
                if (effective > 1 && strategies.Count > 1 && !usedFields.Add(strategy.Field)) continue;

                mutated = Mutate(mutated, strategy, root, rng);
                log.Add(new MutationLogEntry { Mutation = strategy.Mutation, Field = strategy.Field, Description = strategy.Description });
            }

            if (log.Count == 0) continue;
            if (preserveChecksums) mutated = RecomputeChecksums(mutated, root);

            var primary = log[0];
            variants.Add(new FuzzVariant
            {
                Index         = variants.Count,
                OriginalFile  = fileName,
                FormatName    = entry.Name,
                Strategy      = primary.Mutation.ToString(),
                Field         = primary.Field,
                Description   = primary.Description,
                Data          = mutated,
                MutationCount = log.Count,
                MutationLog   = log,
            });
        }

        return variants;
    }

    /// <summary>Async variant — reads the file asynchronously then generates.</summary>
    public static async Task<IReadOnlyList<FuzzVariant>> GenerateAsync(
        IEmbeddedFormatCatalog catalog,
        string inputFile,
        int count = 10,
        string? forcedFormat = null,
        int? seed = null,
        int compound = 1,
        CancellationToken cancellationToken = default)
    {
        byte[] data = await File.ReadAllBytesAsync(inputFile, cancellationToken);
        return Generate(catalog, data, Path.GetFileName(inputFile), count, forcedFormat, seed, compound);
    }

    /// <summary>Async variant from a stream — reads all bytes then generates.</summary>
    public static async Task<IReadOnlyList<FuzzVariant>> GenerateAsync(
        IEmbeddedFormatCatalog catalog,
        Stream stream,
        string fileName,
        int count = 10,
        string? forcedFormat = null,
        int? seed = null,
        int compound = 1,
        CancellationToken cancellationToken = default)
    {
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, cancellationToken);
        return Generate(catalog, ms.ToArray(), fileName, count, forcedFormat, seed, compound);
    }

    /// <summary>Generate variants and return both the list and a <see cref="FuzzReport"/>.</summary>
    public static (IReadOnlyList<FuzzVariant> Variants, FuzzReport Report) GenerateWithReport(
        IEmbeddedFormatCatalog catalog,
        string inputFile,
        int count = 10,
        string? forcedFormat = null,
        int? seed = null,
        int compound = 1)
    {
        byte[] data = File.ReadAllBytes(inputFile);
        return GenerateWithReport(catalog, data, Path.GetFileName(inputFile), count, forcedFormat, seed, compound);
    }

    /// <summary>Generate variants from raw bytes and return both the list and a <see cref="FuzzReport"/>.</summary>
    public static (IReadOnlyList<FuzzVariant> Variants, FuzzReport Report) GenerateWithReport(
        IEmbeddedFormatCatalog catalog,
        byte[] inputData,
        string fileName,
        int count = 10,
        string? forcedFormat = null,
        int? seed = null,
        int compound = 1)
    {
        // Resolve once — shared by variant generation and report field collection
        var (entry, json, error) = Resolve(catalog, inputData, fileName, forcedFormat);
        if (error is not null)
        {
            var errVariant = FuzzVariant.ErrorVariant(fileName, error);
            return ([errVariant], new FuzzReport { FormatName = fileName, TotalVariants = 1, ErrorCount = 1 });
        }

        var variants = GenerateFromResolved(entry!, json!, inputData, fileName, count, seed, compound);

        // Collect all block field names for untested detection (json already parsed above)
        var allFields    = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var fuzzedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var doc = JsonDocument.Parse(json!);
        if (doc.RootElement.TryGetProperty("blocks", out var blocks))
            foreach (var b in blocks.EnumerateArray())
            {
                string? n = b.TryGetProperty("name",    out var nv) ? nv.GetString() : null;
                string? s = b.TryGetProperty("storeAs", out var sv) ? sv.GetString() : null;
                if (!string.IsNullOrEmpty(n)) allFields.Add(n);
                if (!string.IsNullOrEmpty(s)) allFields.Add(s);
            }

        var fieldCoverage = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var stratDist     = new Dictionary<MutationType, int>();
        long totalMutations = 0;
        int ok = 0;

        foreach (var v in variants)
        {
            if (v.IsError) continue;
            ok++;
            totalMutations += v.MutationCount;
            foreach (var log in v.MutationLog)
            {
                fieldCoverage[log.Field] = fieldCoverage.GetValueOrDefault(log.Field) + 1;
                stratDist[log.Mutation]  = stratDist.GetValueOrDefault(log.Mutation)  + 1;
                fuzzedFields.Add(log.Field);
            }
        }

        var report = new FuzzReport
        {
            FormatName                 = entry!.Name,
            TotalVariants              = variants.Count,
            ErrorCount                 = variants.Count - ok,
            FieldCoverage              = fieldCoverage,
            StrategyDistribution       = stratDist,
            UntestedFields             = allFields.Except(fuzzedFields, StringComparer.OrdinalIgnoreCase).Order().ToList(),
            AverageMutationsPerVariant = ok == 0 ? 0 : (double)totalMutations / ok,
            Seed                       = seed,
        };

        return (variants, report);
    }

    // ── Mutation engine ──────────────────────────────────────────────────────

    private static byte[] Mutate(byte[] data, FuzzStrategy strategy, JsonElement root, Random rng)
    {
        byte[] result = (byte[])data.Clone();
        var (offset, length) = ResolveField(strategy.Field, root);

        if (offset < 0 || length <= 0 || offset + length > result.Length)
        {
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
                    int picked = rng.Next(0, enumVals.Count + 5);
                    byte val = picked < enumVals.Count ? (byte)enumVals[picked] : (byte)rng.Next(200, 256);
                    if (offset < result.Length) result[offset] = val;
                }
                break;

            case MutationType.CorruptSignature:
                for (int i = 0; i < Math.Min(length, result.Length - (int)offset); i++)
                    result[(int)offset + i] ^= (byte)rng.Next(1, 255);
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
                    Array.Copy(result, srcEnd, grown, srcEnd + segment.Length, result.Length - srcEnd);
                    result = grown;
                }
                break;

            case MutationType.InsertBytes:
                int insertLen = Math.Max(1, rng.Next(1, Math.Max(2, length)));
                byte[] inserted = new byte[insertLen];
                rng.NextBytes(inserted);
                var withInsert = new byte[result.Length + insertLen];
                Array.Copy(result, withInsert, (int)offset);
                Array.Copy(inserted, 0, withInsert, (int)offset, insertLen);
                Array.Copy(result, (int)offset, withInsert, (int)offset + insertLen, result.Length - (int)offset);
                result = withInsert;
                break;

            case MutationType.SliceRepeat:
                int repeatEnd = (int)offset + length;
                if (repeatEnd <= result.Length && length > 0)
                {
                    int times = rng.Next(2, 5);
                    byte[] slice = result[(int)offset..repeatEnd];
                    byte[] repeated = new byte[result.Length + slice.Length * (times - 1)];
                    Array.Copy(result, repeated, (int)offset);
                    for (int r = 0; r < times; r++)
                        Array.Copy(slice, 0, repeated, (int)offset + r * slice.Length, slice.Length);
                    Array.Copy(result, repeatEnd, repeated, (int)offset + slice.Length * times, result.Length - repeatEnd);
                    result = repeated;
                }
                break;

            case MutationType.NegateField:
                for (int i = (int)offset; i < Math.Min((int)offset + length, result.Length); i++)
                    result[i] ^= 0xFF;
                break;
        }

        return result;
    }

    private static void ApplyBoundaryValue(byte[] data, int offset, int length, Random rng)
    {
        long[] boundaries = [0, 1, 127, 128, 255, 256, 32767, 32768, 65535, 65536, int.MaxValue, (long)uint.MaxValue];
        long chosen = boundaries[rng.Next(boundaries.Length)];
        byte[] bytes = BitConverter.GetBytes(chosen);
        int copy = Math.Min(length, Math.Min(bytes.Length, data.Length - offset));
        Array.Copy(bytes, 0, data, offset, copy);
    }

    private static (long offset, int length) ResolveField(string fieldName, JsonElement root)
    {
        if (!root.TryGetProperty("blocks", out var blocks)) return (-1, 0);
        foreach (var block in blocks.EnumerateArray())
        {
            string? name  = block.TryGetProperty("name",    out var n) ? n.GetString() : null;
            string? store = block.TryGetProperty("storeAs", out var s) ? s.GetString() : null;
            if (!string.Equals(name,  fieldName, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(store, fieldName, StringComparison.OrdinalIgnoreCase)) continue;
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
            string algo       = cs.TryGetProperty("algorithm", out var av) ? av.GetString() ?? "" : "";
            if (!cs.TryGetProperty("storedAt", out var sat)) continue;
            long storedOffset = sat.TryGetProperty("fixedOffset", out var sfo) ? sfo.GetInt64() : -1;
            int  storedLen    = sat.TryGetProperty("length",      out var sl)  ? sl.GetInt32()  : 4;
            if (storedOffset < 0 || storedOffset + storedLen > data.Length) continue;

            long dataOffset = cs.TryGetProperty("dataRange", out var dr)  && dr.TryGetProperty("fixedOffset",  out var dfo) ? dfo.GetInt64() : 0;
            long dataLength = cs.TryGetProperty("dataRange", out var dr2) && dr2.TryGetProperty("fixedLength", out var dfl) ? dfl.GetInt64() : data.Length - dataOffset;
            if (dataOffset < 0 || dataLength <= 0 || dataOffset + dataLength > data.Length) continue;

            byte[] slice    = data[(int)dataOffset..(int)(dataOffset + dataLength)];
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

    private static (EmbeddedFormatEntry? Entry, string? Json, string? Error) Resolve(
        IEmbeddedFormatCatalog catalog, byte[] data, string fileName, string? forcedFormat)
    {
        var entry = forcedFormat is not null
            ? catalog.GetAll().FirstOrDefault(e =>
                e.Name.Equals(forcedFormat, StringComparison.OrdinalIgnoreCase) ||
                e.Extensions.Any(x => x.TrimStart('.').Equals(forcedFormat.TrimStart('.'), StringComparison.OrdinalIgnoreCase)))
            : FormatFileAnalyzer.Analyze(catalog, new MemoryStream(data), Path.GetExtension(fileName))?.Entry;

        if (entry is null) return (null, null, "Could not detect format.");
        string? json = catalog.GetJson(entry.ResourceKey);
        if (json is null) return (null, null, "No full definition for this format.");
        return (entry, json, null);
    }

    private static string ToPascal(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return string.Concat(s.Split('_').Select(p => p.Length > 0 ? char.ToUpper(p[0]) + p[1..] : p));
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
