//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7 (1M context)
//////////////////////////////////////////////
// Project: WpfHexEditor.Core.Definitions
// File: Metadata/FormatDiffFuzzExtensions.cs
// Description: Surfaces the diff{} and fuzz{} sections of .whfmt files to the
//              IDE. Previously consumed only by the whfmt.Analysis and
//              whfmt.Fuzz CLI tools.
// Architecture notes (ADR-038 P7):
//              Model layer only — the diff algorithm and fuzz mutator live in
//              the existing tools. IDE consumers (diff viewer, fuzz panel)
//              read these models to know which fields to compare or mutate.
//////////////////////////////////////////////

using System.Text.Json;
using WpfHexEditor.Core.Contracts;
using WpfHexEditor.Core.Definitions.Models;

namespace WpfHexEditor.Core.Definitions.Metadata;

// ---------------------------------------------------------------------------
// Model types
// ---------------------------------------------------------------------------

/// <summary>Semantic-diff configuration from a format's <c>diff</c> block.</summary>
public sealed record DiffConfig(
    IReadOnlyList<string> KeyFields,
    IReadOnlyList<string> IgnoreFields,
    string? GroupBy,
    string? Note);

/// <summary>Canonical fuzz mutation strategies recognized by whfmt.Fuzz.</summary>
public enum FuzzMutation
{
    Unknown,
    CorruptSignature,
    EnumSweep,
    BoundaryValues,
    BitFlip,
    Overflow,
    RandomBytes,
    /// <summary>Truncate the file to a shorter length (catalog: timestamp/log formats).</summary>
    Truncate,
    /// <summary>Zero out the field's bytes (catalog: checksum/length integrity tests).</summary>
    ZeroField,
}

/// <summary>A single fuzz mutation strategy from a format's <c>fuzz.strategies[]</c> array.</summary>
public sealed record FuzzStrategy(
    string Field,
    FuzzMutation Mutation,
    string MutationRaw,
    double Weight,
    double Rate,
    string? Description);

/// <summary>Fuzz configuration from a format's <c>fuzz</c> block.</summary>
public sealed record FuzzConfig(
    bool PreserveChecksums,
    int MaxMutationsPerFile,
    string? Note,
    IReadOnlyList<FuzzStrategy> Strategies);

// ---------------------------------------------------------------------------
// Extension methods
// ---------------------------------------------------------------------------

/// <summary>Diff + fuzz model extensions on <see cref="EmbeddedFormatEntry"/>.</summary>
public static class FormatDiffFuzzExtensions
{
    /// <summary>
    /// Returns the <c>diff</c> configuration. Returns null when the block is absent.
    /// </summary>
    public static DiffConfig? GetDiffConfig(
        this EmbeddedFormatEntry entry, IEmbeddedFormatCatalog catalog)
    {
        using var doc = JsonDocument.Parse(catalog.GetJson(entry.ResourceKey), WhfmtJsonOptions.Jsonc);
        var root = doc.RootElement;
        if (!root.TryGetProperty("diff", out var d) || d.ValueKind != JsonValueKind.Object) return null;

        return new DiffConfig(
            KeyFields:    d.TryGetProperty("keyFields",    out var k) ? ReadStringArray(k) : [],
            IgnoreFields: d.TryGetProperty("ignoreFields", out var i) ? ReadStringArray(i) : [],
            GroupBy:      StrN(d, "groupBy"),
            Note:         StrN(d, "note"));
    }

    /// <summary>
    /// Returns the <c>fuzz</c> configuration. Returns null when the block is absent.
    /// </summary>
    public static FuzzConfig? GetFuzzConfig(
        this EmbeddedFormatEntry entry, IEmbeddedFormatCatalog catalog)
    {
        using var doc = JsonDocument.Parse(catalog.GetJson(entry.ResourceKey), WhfmtJsonOptions.Jsonc);
        var root = doc.RootElement;
        if (!root.TryGetProperty("fuzz", out var f) || f.ValueKind != JsonValueKind.Object) return null;

        bool preserve = f.TryGetProperty("preserveChecksums", out var pc) && pc.ValueKind == JsonValueKind.True;
        int  maxMut   = f.TryGetProperty("maxMutationsPerFile", out var mm) && mm.ValueKind == JsonValueKind.Number && mm.TryGetInt32(out var mmi)
                          ? mmi : 1;

        var strategies = new List<FuzzStrategy>();
        if (f.TryGetProperty("strategies", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var s in arr.EnumerateArray())
            {
                if (s.ValueKind != JsonValueKind.Object) continue;
                var field = Str(s, "field");
                if (string.IsNullOrEmpty(field)) continue;
                var raw   = Str(s, "mutation");
                var w     = s.TryGetProperty("weight", out var wv) && wv.ValueKind == JsonValueKind.Number ? wv.GetDouble() : 1.0;
                var rate  = s.TryGetProperty("rate",   out var rv) && rv.ValueKind == JsonValueKind.Number ? rv.GetDouble() : 1.0;
                strategies.Add(new FuzzStrategy(field, ParseMutation(raw), raw, w, rate, StrN(s, "description")));
            }
        }

        return new FuzzConfig(preserve, maxMut, StrN(f, "note"), strategies);
    }

    // ----- Helpers ------------------------------------------------------------

    private static FuzzMutation ParseMutation(string raw) => raw switch
    {
        "corrupt_signature" => FuzzMutation.CorruptSignature,
        "enum_sweep"        => FuzzMutation.EnumSweep,
        "boundary_values"   => FuzzMutation.BoundaryValues,
        "bit_flip"          => FuzzMutation.BitFlip,
        "overflow"          => FuzzMutation.Overflow,
        "random_bytes"      => FuzzMutation.RandomBytes,
        "truncate"          => FuzzMutation.Truncate,
        "zero_field"        => FuzzMutation.ZeroField,
        _                   => FuzzMutation.Unknown,
    };

    private static string Str(JsonElement el, string prop)
        => el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? "" : "";

    private static string? StrN(JsonElement el, string prop)
        => el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() : null;

    private static IReadOnlyList<string> ReadStringArray(JsonElement el)
    {
        if (el.ValueKind != JsonValueKind.Array) return [];
        var list = new List<string>(el.GetArrayLength());
        foreach (var item in el.EnumerateArray())
            if (item.ValueKind == JsonValueKind.String && item.GetString() is { Length: > 0 } s)
                list.Add(s);
        return list;
    }
}
