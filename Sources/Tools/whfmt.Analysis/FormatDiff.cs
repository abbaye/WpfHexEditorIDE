// ==========================================================
// Project: whfmt.Analysis
// File: FormatDiff.cs
// Description: Public entry point — field-level semantic diff between two
//              binary files using whfmt diff definitions.
// ==========================================================

using System.Text.Json;
using WpfHexEditor.Core.Definitions;
using WpfHexEditor.Core.Definitions.Matching;
using WpfHexEditor.Core.Contracts;

namespace WhfmtAnalysis;

/// <summary>Compares two binary files at the field level using whfmt format definitions.</summary>
public static class FormatDiff
{
    /// <summary>
    /// Compare two files semantically using their shared whfmt format definition.
    /// Returns a <see cref="DiffResult"/> with field-level changes, ignored fields,
    /// and grouped entry comparisons.
    /// </summary>
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
            ? MatchForced(catalog, forcedFormat, dataA, nameA)
            : FormatFileAnalyzer.Analyze(catalog, new MemoryStream(dataA), Path.GetExtension(nameA));

        var matchB = forcedFormat is not null
            ? MatchForced(catalog, forcedFormat, dataB, nameB)
            : FormatFileAnalyzer.Analyze(catalog, new MemoryStream(dataB), Path.GetExtension(nameB));

        var result = new DiffResult
        {
            FileA         = nameA,
            FileB         = nameB,
            SizeA         = dataA.Length,
            SizeB         = dataB.Length,
            FormatName    = matchA?.Entry.Name ?? matchB?.Entry.Name ?? "Unknown",
            FormatDetectedA = matchA?.Entry.Name ?? "Unknown",
            FormatDetectedB = matchB?.Entry.Name ?? "Unknown",
            FormatsMatch  = matchA?.Entry.Name == matchB?.Entry.Name,
        };

        var entry = matchA?.Entry ?? matchB?.Entry;
        if (entry is null)
        {
            result.Error = "Could not detect format for either file.";
            return result;
        }

        var json = catalog.GetJson(entry.ResourceKey);
        if (json is null)
        {
            result.Error = "No full definition available for this format.";
            return result;
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Parse diff configuration
        var diffConfig = ParseDiffConfig(root);
        result.KeyFields    = diffConfig.KeyFields;
        result.IgnoreFields = diffConfig.IgnoreFields;
        result.GroupBy      = diffConfig.GroupBy;

        // Extract fields from both files using variables block + blocks
        var varsA = ExtractVariables(root, dataA);
        var varsB = ExtractVariables(root, dataB);

        // Compare key fields
        foreach (var field in diffConfig.KeyFields)
        {
            varsA.TryGetValue(field, out var valA);
            varsB.TryGetValue(field, out var valB);

            string strA = valA?.ToString() ?? "(not found)";
            string strB = valB?.ToString() ?? "(not found)";

            bool equal = string.Equals(strA, strB, StringComparison.OrdinalIgnoreCase);
            result.FieldChanges.Add(new FieldChange
            {
                FieldName = field,
                ValueA    = strA,
                ValueB    = strB,
                IsChanged = !equal,
                IsIgnored = false,
            });
        }

        // Report ignored fields
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

        // Raw size delta
        result.RawByteDelta = dataB.Length - dataA.Length;
        result.IsIdentical  = result.FieldChanges.All(c => c.IsIgnored || !c.IsChanged)
                           && dataA.Length == dataB.Length;

        return result;
    }

    // ── Private ─────────────────────────────────────────────────────────────

    private static (List<string> KeyFields, List<string> IgnoreFields, string? GroupBy) ParseDiffConfig(JsonElement root)
    {
        var key    = new List<string>();
        var ignore = new List<string>();
        string? groupBy = null;

        if (root.TryGetProperty("diff", out var diff))
        {
            if (diff.TryGetProperty("keyFields",    out var kf)) key.AddRange(kf.EnumerateArray().Select(e => e.GetString() ?? ""));
            if (diff.TryGetProperty("ignoreFields", out var ig)) ignore.AddRange(ig.EnumerateArray().Select(e => e.GetString() ?? ""));
            if (diff.TryGetProperty("groupBy",      out var gb)) groupBy = gb.GetString();
        }

        // Fallback: use variables block keys as key fields
        if (key.Count == 0 && root.TryGetProperty("variables", out var vars))
            foreach (var v in vars.EnumerateObject())
                key.Add(v.Name);

        return (key, ignore, groupBy);
    }

    private static Dictionary<string, object> ExtractVariables(JsonElement root, byte[] data)
    {
        var vars = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        // Seed from variables block
        if (root.TryGetProperty("variables", out var varBlock))
            foreach (var v in varBlock.EnumerateObject())
                vars[v.Name] = v.Value.ValueKind == JsonValueKind.Number
                    ? (object)v.Value.GetInt64()
                    : (v.Value.GetString() ?? "");

        // Simple field extraction from blocks (storeAs fields only)
        if (root.TryGetProperty("blocks", out var blocks))
            ExtractFromBlocks(blocks, data, vars);

        return vars;
    }

    private static void ExtractFromBlocks(JsonElement blocks, byte[] data, Dictionary<string, object> vars)
    {
        foreach (var block in blocks.EnumerateArray())
        {
            if (!block.TryGetProperty("storeAs", out var sa)) continue;
            string varName = sa.GetString() ?? "";
            if (string.IsNullOrWhiteSpace(varName)) continue;

            long offset = block.TryGetProperty("offset", out var off) ? off.ValueKind == JsonValueKind.Number ? off.GetInt64() : 0 : 0;
            int  length = block.TryGetProperty("length", out var len) ? len.ValueKind == JsonValueKind.Number ? len.GetInt32() : 0 : 0;
            string vt   = block.TryGetProperty("valueType", out var vtEl) ? vtEl.GetString() ?? "" : "";

            if (offset < 0 || length <= 0 || offset + length > data.Length) continue;

            byte[] slice = data[(int)offset..(int)(offset + length)];
            vars[varName] = ReadValue(slice, vt);
        }
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
        _        => BitConverter.ToString(bytes).Replace("-", "")
    };

    private static FormatMatchResult? MatchForced(IEmbeddedFormatCatalog catalog, string format, byte[] data, string name)
    {
        var entry = catalog.GetAll().FirstOrDefault(e =>
            e.Name.Equals(format, StringComparison.OrdinalIgnoreCase) ||
            e.Extensions.Any(x => x.TrimStart('.').Equals(format.TrimStart('.'), StringComparison.OrdinalIgnoreCase)));
        return entry is null ? null : new FormatMatchResult(entry, 1.0, WpfHexEditor.Core.Contracts.MatchSource.Extension, 1.0);
    }
}
