// ==========================================================
// Project: whfmt.CodeGen
// File: Commands/DumpCommand.cs
// Description: `whfmt-codegen dump` — parses a binary file and displays structured field values.
// Architecture: Reads .whfmt blocks → seeks to each offset → renders table with decoded values.
// ==========================================================

using System.CommandLine;
using System.Text;
using System.Text.Json;
using WpfHexEditor.Core.Definitions;
using WpfHexEditor.Core.Definitions.Matching;

namespace WhfmtCodeGen.Commands;

internal static class DumpCommand
{
    internal static Command Build()
    {
        var fileArg    = new Argument<string>("file", "Binary file to parse and dump.");
        var formatOpt  = new Option<string?>(["--format",  "-f"], "Force format (name or extension).");
        var verboseOpt = new Option<bool>   (["--verbose", "-v"], "Show all fields including padding/reserved.");
        var hexOpt     = new Option<bool>   (["--hex",     "-x"], "Show raw hex bytes for all fields.");
        var limitOpt   = new Option<int>    (["--limit",   "-l"], () => 64, "Max byte[] fields to show as hex (default 64).");

        var cmd = new Command("dump", "Parse a binary file and display structured field values from its .whfmt definition.")
        {
            fileArg, formatOpt, verboseOpt, hexOpt, limitOpt
        };

        cmd.SetHandler(async (file, format, verbose, showHex, limit) =>
        {
            if (!File.Exists(file))
            {
                Console.Error.WriteLine($"  [NOT FOUND] {file}");
                Environment.Exit(2);
                return;
            }

            var catalog = EmbeddedFormatCatalog.Instance;
            var entry = format is not null
                ? catalog.GetAll().FirstOrDefault(e =>
                    e.Name.Equals(format, StringComparison.OrdinalIgnoreCase) ||
                    e.Extensions.Any(x => x.TrimStart('.').Equals(format.TrimStart('.'), StringComparison.OrdinalIgnoreCase)))
                : FormatFileAnalyzer.Analyze(catalog, File.OpenRead(file), Path.GetExtension(file))?.Entry;

            if (entry is null)
            {
                Console.Error.WriteLine($"  [UNKNOWN FORMAT] {Path.GetFileName(file)}");
                Console.Error.WriteLine("  Use --format <name> to force format detection.");
                Environment.Exit(2);
                return;
            }

            string? json = catalog.GetJson(entry.ResourceKey);
            if (json is null)
            {
                Console.Error.WriteLine($"  [NO DEFINITION] {entry.Name}");
                Environment.Exit(2);
                return;
            }

            byte[] data = await File.ReadAllBytesAsync(file);

            Console.WriteLine();
            Console.WriteLine($"  File    : {Path.GetFileName(file)}  ({data.Length:N0} bytes)");
            Console.WriteLine($"  Format  : {entry.Name}");
            Console.WriteLine();

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var blocks    = ParseBlocks(root);
            var checksums = ParseChecksums(root);

            // Render field table
            const int colField  = 26;
            const int colOffset = 8;
            const int colLen    = 5;
            const int colHex    = 24;
            const int colValue  = 28;

            string header = $"  {"Field",-colField} {"Offset",colOffset}  {"Len",colLen}  {"Hex",-colHex}  {"Interpreted",-colValue}";
            Console.WriteLine(header);
            Console.WriteLine("  " + new string('─', header.Length - 2));

            foreach (var b in blocks)
            {
                if (string.IsNullOrEmpty(b.PropertyName)) continue;

                bool isPadding = b.Name.Contains("reserved", StringComparison.OrdinalIgnoreCase)
                              || b.Name.Contains("padding",  StringComparison.OrdinalIgnoreCase)
                              || b.Name.Contains("unused",   StringComparison.OrdinalIgnoreCase);
                if (isPadding && !verbose) continue;
                if (b.Offset + b.Length > data.Length) continue;

                byte[] raw = data[(int)b.Offset..(int)(b.Offset + b.Length)];
                string hexStr = raw.Length <= limit
                    ? string.Join(" ", raw.Take(Math.Min(raw.Length, 8)).Select(x => x.ToString("X2")))
                      + (raw.Length > 8 ? $" +{raw.Length - 8}" : "")
                    : $"[{raw.Length} bytes]";

                string interpreted = InterpretField(b, raw, data);
                string sigMark     = b.IsSignature ? CheckSignature(b, raw) : "";

                string fieldLabel = (b.PropertyName.Length > colField - 1)
                    ? b.PropertyName[..(colField - 2)] + "…"
                    : b.PropertyName;

                Console.WriteLine($"  {fieldLabel,-colField} {b.Offset,colOffset}  {b.Length,colLen}  {hexStr,-colHex}  {interpreted,-colValue} {sigMark}");
            }

            // Checksum verification
            if (checksums.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("  Checksums:");
                foreach (var cs in checksums)
                    VerifyChecksum(data, cs);
            }

            Console.WriteLine();
        },
        fileArg, formatOpt, verboseOpt, hexOpt, limitOpt);

        return cmd;
    }

    // ── Block/Checksum models ────────────────────────────────────────────────

    private sealed class BlockDef
    {
        public string Name        { get; init; } = "";
        public string StoreAs     { get; init; } = "";
        public long   Offset      { get; init; }
        public int    Length      { get; init; }
        public string Type        { get; init; } = "bytes";
        public string? Endian     { get; init; }
        public bool   IsSignature { get; init; }
        public byte[]? ExpectedBytes { get; init; }
        public Dictionary<string, string> ValueMap { get; init; } = [];
        public string  Description { get; init; } = "";

        public string PropertyName => ToPascal(string.IsNullOrEmpty(StoreAs) ? Name : StoreAs);
    }

    private sealed class ChecksumDef
    {
        public string Algorithm   { get; init; } = "";
        public long   StoredOffset { get; init; }
        public int    StoredLength { get; init; }
        public long   DataOffset   { get; init; }
        public long   DataLength   { get; init; }
    }

    private static List<BlockDef> ParseBlocks(JsonElement root)
    {
        var list = new List<BlockDef>();
        if (!root.TryGetProperty("blocks", out var blocks)) return list;
        foreach (var b in blocks.EnumerateArray())
        {
            string name    = b.TryGetProperty("name",        out var n)  ? n.GetString()  ?? "" : "";
            string storeAs = b.TryGetProperty("storeAs",     out var s)  ? s.GetString()  ?? "" : "";
            long   offset  = b.TryGetProperty("offset",      out var o)  && o.ValueKind == JsonValueKind.Number ? o.GetInt64() : 0;
            int    length  = b.TryGetProperty("length",      out var l)  && l.ValueKind == JsonValueKind.Number ? l.GetInt32() : 1;
            string type    = b.TryGetProperty("type",        out var t)  ? t.GetString()  ?? "bytes" : "bytes";
            string? endian = b.TryGetProperty("endian",      out var e)  ? e.GetString()  : null;
            bool   isSig   = b.TryGetProperty("isSignature", out var sg) && sg.GetBoolean();
            string desc    = b.TryGetProperty("description", out var ds) ? ds.GetString() ?? "" : "";

            byte[]? expectedBytes = null;
            if (isSig && b.TryGetProperty("value", out var vEl) && vEl.ValueKind == JsonValueKind.String)
            {
                var hex = vEl.GetString()?.Replace(" ", "").Replace("0x", "").Replace("-", "") ?? "";
                if (hex.Length % 2 == 0 && hex.Length > 0)
                    expectedBytes = Convert.FromHexString(hex);
            }

            var vm = new Dictionary<string, string>();
            if (b.TryGetProperty("valueMap", out var vmEl))
                foreach (var kv in vmEl.EnumerateObject())
                    vm[kv.Name] = kv.Value.GetString() ?? kv.Name;

            if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(storeAs)) continue;
            list.Add(new BlockDef
            {
                Name = name, StoreAs = storeAs, Offset = offset, Length = length,
                Type = type, Endian = endian, IsSignature = isSig,
                ExpectedBytes = expectedBytes, ValueMap = vm, Description = desc
            });
        }
        return list;
    }

    private static List<ChecksumDef> ParseChecksums(JsonElement root)
    {
        var list = new List<ChecksumDef>();
        if (!root.TryGetProperty("checksums", out var cks)) return list;
        foreach (var ck in cks.EnumerateArray())
        {
            string algo    = ck.TryGetProperty("algorithm", out var a) ? a.GetString() ?? "" : "";
            long storedOff = ck.TryGetProperty("storedAt",  out var sat)  && sat.TryGetProperty("fixedOffset", out var sfo) ? sfo.GetInt64() : -1;
            int  storedLen = ck.TryGetProperty("storedAt",  out var sat2) && sat2.TryGetProperty("length",     out var sl)  ? sl.GetInt32()  : 4;
            long dataOff   = ck.TryGetProperty("dataRange", out var dr)   && dr.TryGetProperty("fixedOffset",  out var dfo) ? dfo.GetInt64() : 0;
            long dataLen   = ck.TryGetProperty("dataRange", out var dr2)  && dr2.TryGetProperty("fixedLength", out var dfl) ? dfl.GetInt64() : -1;
            if (storedOff < 0) continue;
            list.Add(new ChecksumDef { Algorithm = algo, StoredOffset = storedOff, StoredLength = storedLen, DataOffset = dataOff, DataLength = dataLen });
        }
        return list;
    }

    // ── Field interpretation ─────────────────────────────────────────────────

    private static string InterpretField(BlockDef b, byte[] raw, byte[] fullData)
    {
        string csType = MapType(b.Type, b.Length);
        object? scalar = csType switch
        {
            "byte"   => raw[0],
            "sbyte"  => (sbyte)raw[0],
            "ushort" => b.Endian == "big" ? (object)BitConverter.ToUInt16([raw[1], raw[0]]) : BitConverter.ToUInt16(raw),
            "short"  => b.Endian == "big" ? (object)(short)BitConverter.ToUInt16([raw[1], raw[0]]) : BitConverter.ToInt16(raw),
            "uint"   => b.Endian == "big" ? (object)BSwap32(BitConverter.ToUInt32(raw)) : BitConverter.ToUInt32(raw),
            "int"    => b.Endian == "big" ? (object)(int)BSwap32(BitConverter.ToUInt32(raw)) : BitConverter.ToInt32(raw),
            "ulong"  => b.Endian == "big" ? (object)BSwap64(BitConverter.ToUInt64(raw)) : BitConverter.ToUInt64(raw),
            "long"   => b.Endian == "big" ? (object)(long)BSwap64(BitConverter.ToUInt64(raw)) : BitConverter.ToInt64(raw),
            "float"  => (object)BitConverter.ToSingle(raw),
            "double" => (object)BitConverter.ToDouble(raw),
            "string" => Encoding.Latin1.GetString(raw).TrimEnd('\0'),
            _        => null,
        };

        if (scalar is null)
        {
            // Try UTF-8 string representation
            bool isPrintable = raw.All(x => x >= 0x20 && x < 0x7F);
            return isPrintable ? $"\"{Encoding.ASCII.GetString(raw).TrimEnd('\0')}\"" : $"[{raw.Length} bytes]";
        }

        string label = scalar.ToString() ?? "";

        // ValueMap lookup
        if (b.ValueMap.Count > 0 && b.ValueMap.TryGetValue(label, out var mapped))
            label = $"{scalar} ({mapped})";

        return label;
    }

    private static string CheckSignature(BlockDef b, byte[] raw)
    {
        if (b.ExpectedBytes is null) return "◆ sig";
        return raw.SequenceEqual(b.ExpectedBytes) ? "✓ sig" : "✗ sig MISMATCH";
    }

    private static void VerifyChecksum(byte[] data, ChecksumDef cs)
    {
        long dataLen = cs.DataLength > 0 ? cs.DataLength : data.Length - cs.DataOffset;
        if (cs.StoredOffset + cs.StoredLength > data.Length || cs.DataOffset + dataLen > data.Length) return;

        byte[] slice    = data[(int)cs.DataOffset..(int)(cs.DataOffset + dataLen)];
        byte[] stored   = data[(int)cs.StoredOffset..(int)(cs.StoredOffset + cs.StoredLength)];
        byte[]? computed = ComputeChecksum(cs.Algorithm, slice);

        if (computed is null)
        {
            Console.WriteLine($"    {cs.Algorithm,-10} @ {cs.StoredOffset}  [unknown algorithm]");
            return;
        }

        byte[] storedTrimmed  = stored[..Math.Min(stored.Length, computed.Length)];
        byte[] computedTrimmed= computed[..Math.Min(computed.Length, stored.Length)];
        bool match = storedTrimmed.SequenceEqual(computedTrimmed);

        string storedHex   = string.Join("", storedTrimmed.Select(x => x.ToString("X2")));
        string computedHex = string.Join("", computedTrimmed.Select(x => x.ToString("X2")));
        string status      = match ? "✓" : "✗ MISMATCH";
        Console.WriteLine($"    {cs.Algorithm.ToUpper(),-10} @ {cs.StoredOffset,-6}  stored={storedHex}  computed={computedHex}  {status}");
    }

    private static byte[]? ComputeChecksum(string algorithm, byte[] data) => algorithm.ToLowerInvariant() switch
    {
        "crc32"  => BitConverter.GetBytes(Crc32(data)),
        "md5"    => System.Security.Cryptography.MD5.HashData(data),
        "sha1"   => System.Security.Cryptography.SHA1.HashData(data),
        "sha256" => System.Security.Cryptography.SHA256.HashData(data),
        _        => null,
    };

    private static uint Crc32(byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (var b in data) crc = (crc >> 8) ^ _crc32Table[(crc & 0xFF) ^ b];
        return ~crc;
    }
    private static readonly uint[] _crc32Table = BuildCrc32Table();
    private static uint[] BuildCrc32Table()
    {
        var t = new uint[256];
        for (uint i = 0; i < 256; i++) { uint c = i; for (int j = 8; j > 0; j--) c = (c & 1) != 0 ? (c >> 1) ^ 0xEDB88320 : c >> 1; t[i] = c; }
        return t;
    }

    private static string MapType(string whfmtType, int length) => whfmtType.ToLowerInvariant() switch
    {
        "uint8"  or "byte"   => "byte",
        "uint16" or "ushort" => "ushort",
        "uint32" or "uint"   => "uint",
        "uint64" or "ulong"  => "ulong",
        "int8"   or "sbyte"  => "sbyte",
        "int16"  or "short"  => "short",
        "int32"  or "int"    => "int",
        "int64"  or "long"   => "long",
        "float"  or "float32"=> "float",
        "double" or "float64"=> "double",
        "string" or "ascii"  or "utf8" or "utf-8" => "string",
        _ => "byte[]",
    };

    private static uint   BSwap32(uint v)   => (v << 24) | ((v & 0xFF00) << 8) | ((v >> 8) & 0xFF00) | (v >> 24);
    private static ulong  BSwap64(ulong v)  => ((ulong)BSwap32((uint)v) << 32) | BSwap32((uint)(v >> 32));

    private static string ToPascal(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var sb = new StringBuilder();
        bool upper = true;
        foreach (char c in s)
        {
            if (c == '_' || c == '-' || c == ' ') { upper = true; continue; }
            sb.Append(upper ? char.ToUpper(c) : c);
            upper = false;
        }
        return sb.ToString();
    }
}
