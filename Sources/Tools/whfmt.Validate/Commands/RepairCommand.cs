// ==========================================================
// Project: whfmt.Validate
// File: Commands/RepairCommand.cs
// Description: `whfmt repair` — applies repair rules from .whfmt definitions to fix binary files.
// Architecture: Reads repair[] block from whfmt JSON, applies actions in order, writes output.
// ==========================================================

using System.CommandLine;
using System.Security.Cryptography;
using System.Text.Json;
using WpfHexEditor.Core.Definitions;
using WpfHexEditor.Core.Definitions.Matching;

namespace WhfmtValidate.Commands;

internal static class RepairCommand
{
    internal static Command Build()
    {
        var filesArg   = new Argument<string[]>("files",  "File(s) to repair.") { Arity = ArgumentArity.OneOrMore };
        var formatOpt  = new Option<string?>(["--format",   "-f"], "Force format (name or extension).");
        var outputOpt  = new Option<string?>(["--output",   "-o"], "Output path (single file) or directory (multiple files). Default: overwrite in-place.");
        var dryRunOpt  = new Option<bool>   (["--dry-run",  "-d"], "Show what would change without writing.");
        var verboseOpt = new Option<bool>   (["--verbose",  "-v"], "Show each repair action applied.");

        var cmd = new Command("repair", "Apply format-aware repair rules to fix corrupted binary files.")
        {
            filesArg, formatOpt, outputOpt, dryRunOpt, verboseOpt
        };

        cmd.SetHandler(async (files, format, output, dryRun, verbose) =>
        {
            var catalog = EmbeddedFormatCatalog.Instance;
            int repaired = 0, skipped = 0, failed = 0;

            bool multiFile = files.Length > 1;
            bool outputIsDir = output is not null && (Directory.Exists(output) || (multiFile && !Path.HasExtension(output)));

            foreach (var file in files)
            {
                if (!File.Exists(file))
                {
                    Console.Error.WriteLine($"  [NOT FOUND] {file}");
                    failed++;
                    continue;
                }

                var entry = format is not null
                    ? catalog.GetAll().FirstOrDefault(e =>
                        e.Name.Equals(format, StringComparison.OrdinalIgnoreCase) ||
                        e.Extensions.Any(x => x.TrimStart('.').Equals(format.TrimStart('.'), StringComparison.OrdinalIgnoreCase)))
                    : FormatFileAnalyzer.Analyze(catalog, File.OpenRead(file), Path.GetExtension(file))?.Entry;

                if (entry is null)
                {
                    Console.Error.WriteLine($"  [UNKNOWN FORMAT] {file}");
                    skipped++;
                    continue;
                }

                var json = catalog.GetJson(entry.ResourceKey);
                if (json is null)
                {
                    Console.Error.WriteLine($"  [NO DEFINITION] {entry.Name}");
                    skipped++;
                    continue;
                }

                byte[] data = await File.ReadAllBytesAsync(file);
                var actions = ParseRepairRules(json);

                if (actions.Count == 0)
                {
                    Console.WriteLine($"  [NO RULES] {Path.GetFileName(file)} — format '{entry.Name}' has no repair rules.");
                    skipped++;
                    continue;
                }

                var log = new List<string>();
                byte[] repaired_data = ApplyRepairs(data, actions, json, log);

                if (verbose || dryRun)
                {
                    Console.WriteLine($"  {Path.GetFileName(file)} ({entry.Name}):");
                    foreach (var l in log) Console.WriteLine($"    {l}");
                }

                if (dryRun)
                {
                    Console.WriteLine($"  [DRY RUN] Would apply {log.Count} repair(s) to {Path.GetFileName(file)}.");
                    repaired++;
                    continue;
                }

                string destPath = ResolveOutputPath(file, output, outputIsDir);
                if (destPath != file) Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                await File.WriteAllBytesAsync(destPath, repaired_data);

                Console.WriteLine($"  [REPAIRED] {Path.GetFileName(file)} → {destPath} ({log.Count} action(s))");
                repaired++;
            }

            Console.WriteLine();
            Console.WriteLine($"  Repaired: {repaired}  Skipped: {skipped}  Failed: {failed}");
            Environment.Exit(failed > 0 ? 2 : 0);
        },
        filesArg, formatOpt, outputOpt, dryRunOpt, verboseOpt);

        return cmd;
    }

    // ── Repair rule model ────────────────────────────────────────────────────

    private sealed class RepairRule
    {
        public string Name        { get; init; } = "";
        public string Action      { get; init; } = "";
        public string? Field      { get; init; }
        public string? Algorithm  { get; init; }
        public byte[]? Value      { get; init; }
        public long    PadTo      { get; init; }
        public byte    PadByte    { get; init; }
        public string  Description { get; init; } = "";
    }

    private static List<RepairRule> ParseRepairRules(string json)
    {
        var list = new List<RepairRule>();
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("repair", out var repairArr)) return list;

        foreach (var r in repairArr.EnumerateArray())
        {
            string name   = r.TryGetProperty("name",        out var n) ? n.GetString() ?? "" : "";
            string action = r.TryGetProperty("action",      out var a) ? a.GetString() ?? "" : "";
            string? field = r.TryGetProperty("field",       out var f) ? f.GetString()       : null;
            string? algo  = r.TryGetProperty("algorithm",   out var al) ? al.GetString()     : null;
            string  desc  = r.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";

            byte[]? value = null;
            if (r.TryGetProperty("value", out var vEl) && vEl.ValueKind == JsonValueKind.String)
            {
                var hex = vEl.GetString()?.Replace(" ", "").Replace("0x", "").Replace("-", "") ?? "";
                if (hex.Length % 2 == 0 && hex.Length > 0)
                    value = Convert.FromHexString(hex);
            }

            long padTo = r.TryGetProperty("padTo", out var pt) && pt.ValueKind == JsonValueKind.Number ? pt.GetInt64() : 0;
            byte padByte = r.TryGetProperty("padByte", out var pb) && pb.ValueKind == JsonValueKind.Number ? Convert.ToByte(pb.GetInt32()) : (byte)0x00;

            list.Add(new RepairRule { Name = name, Action = action, Field = field, Algorithm = algo, Value = value, PadTo = padTo, PadByte = padByte, Description = desc });
        }
        return list;
    }

    // ── Repair engine ────────────────────────────────────────────────────────

    private static byte[] ApplyRepairs(byte[] data, List<RepairRule> rules, string json, List<string> log)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        foreach (var rule in rules)
        {
            switch (rule.Action.ToLowerInvariant())
            {
                case "set_value" when rule.Value is not null && rule.Field is not null:
                {
                    var (off, len) = ResolveField(rule.Field, root);
                    if (off >= 0 && off + Math.Min(rule.Value.Length, len) <= data.Length)
                    {
                        int copy = Math.Min(rule.Value.Length, Math.Min(len, data.Length - (int)off));
                        Array.Copy(rule.Value, 0, data, off, copy);
                        log.Add($"set_value '{rule.Field}' @ offset {off} ({copy} bytes)");
                    }
                    break;
                }

                case "recompute_checksum":
                {
                    data = RecomputeChecksums(data, root, rule.Algorithm, log);
                    break;
                }

                case "zero_field" when rule.Field is not null:
                {
                    var (off, len) = ResolveField(rule.Field, root);
                    if (off >= 0 && off + len <= data.Length)
                    {
                        Array.Clear(data, (int)off, len);
                        log.Add($"zero_field '{rule.Field}' @ offset {off} ({len} bytes)");
                    }
                    break;
                }

                case "truncate" when rule.Field is not null:
                {
                    var (off, _) = ResolveField(rule.Field, root);
                    if (off > 0 && off < data.Length)
                    {
                        data = data[..(int)off];
                        log.Add($"truncate at '{rule.Field}' offset {off}");
                    }
                    break;
                }

                case "pad":
                {
                    if (rule.PadTo > 0 && data.Length < rule.PadTo)
                    {
                        var padded = new byte[rule.PadTo];
                        Array.Copy(data, padded, data.Length);
                        if (rule.PadByte != 0)
                            for (long i = data.Length; i < rule.PadTo; i++)
                                padded[i] = rule.PadByte;
                        log.Add($"pad {data.Length} → {rule.PadTo} bytes (0x{rule.PadByte:X2})");
                        data = padded;
                    }
                    break;
                }

                case "rebuild_index":
                    log.Add($"rebuild_index '{rule.Name}' — structural rebuild not supported in this version");
                    break;
            }
        }
        return data;
    }

    private static (long offset, int length) ResolveField(string fieldName, JsonElement root)
    {
        if (!root.TryGetProperty("blocks", out var blocks)) return (-1, 0);
        foreach (var block in blocks.EnumerateArray())
        {
            string? name  = block.TryGetProperty("name",    out var n) ? n.GetString() : null;
            string? store = block.TryGetProperty("storeAs", out var s) ? s.GetString() : null;
            if (!string.Equals(name, fieldName, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(store, fieldName, StringComparison.OrdinalIgnoreCase)) continue;

            long off = block.TryGetProperty("offset", out var ov) && ov.ValueKind == JsonValueKind.Number ? ov.GetInt64() : 0;
            int  len = block.TryGetProperty("length", out var lv) && lv.ValueKind == JsonValueKind.Number ? lv.GetInt32() : 1;
            return (off, len);
        }
        return (-1, 0);
    }

    private static byte[] RecomputeChecksums(byte[] data, JsonElement root, string? onlyAlgorithm, List<string> log)
    {
        if (!root.TryGetProperty("checksums", out var checksums)) return data;
        foreach (var cs in checksums.EnumerateArray())
        {
            string algo = cs.TryGetProperty("algorithm", out var av) ? av.GetString() ?? "" : "";
            if (onlyAlgorithm is not null && !algo.Equals(onlyAlgorithm, StringComparison.OrdinalIgnoreCase)) continue;

            if (!cs.TryGetProperty("storedAt", out var sat)) continue;
            long storedOff = sat.TryGetProperty("fixedOffset", out var sfo) ? sfo.GetInt64() : -1;
            int  storedLen = sat.TryGetProperty("length",      out var sl)  ? sl.GetInt32()  : 4;
            if (storedOff < 0 || storedOff + storedLen > data.Length) continue;

            long dataOff = cs.TryGetProperty("dataRange", out var dr)  && dr.TryGetProperty("fixedOffset",  out var dfo) ? dfo.GetInt64() : 0;
            long dataLen = cs.TryGetProperty("dataRange", out var dr2) && dr2.TryGetProperty("fixedLength", out var dfl) ? dfl.GetInt64() : data.Length - dataOff;
            if (dataOff < 0 || dataLen <= 0 || dataOff + dataLen > data.Length) continue;

            byte[] slice = data[(int)dataOff..(int)(dataOff + dataLen)];
            byte[]? computed = ComputeChecksumBytes(algo, slice);
            if (computed is null) { log.Add($"recompute_checksum: unknown algorithm '{algo}'"); continue; }

            int copy = Math.Min(storedLen, Math.Min(computed.Length, data.Length - (int)storedOff));
            Array.Copy(computed, 0, data, storedOff, copy);
            log.Add($"recompute_checksum {algo.ToUpper()} @ offset {storedOff} ({copy} bytes)");
        }
        return data;
    }

    private static byte[]? ComputeChecksumBytes(string algorithm, byte[] data)
    {
        return algorithm.ToLowerInvariant() switch
        {
            "md5"    => MD5.HashData(data),
            "sha1"   => SHA1.HashData(data),
            "sha256" => SHA256.HashData(data),
            "crc32"  => BitConverter.GetBytes(Crc32(data)),
            _        => null,
        };
    }

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

    private static string ResolveOutputPath(string inputFile, string? output, bool outputIsDir)
    {
        if (output is null) return inputFile; // in-place
        if (outputIsDir)
        {
            Directory.CreateDirectory(output);
            return Path.Combine(output, Path.GetFileName(inputFile));
        }
        return output;
    }
}
