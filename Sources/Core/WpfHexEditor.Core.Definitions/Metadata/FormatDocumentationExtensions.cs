//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7 (1M context)
//////////////////////////////////////////////
// Project: WpfHexEditor.Core.Definitions
// File: Metadata/FormatDocumentationExtensions.cs
// Description: Extension methods exposing the documentary fields of a .whfmt
//              that the runtime previously ignored: Software, UseCases,
//              references, formatRelationships, plus the inspector and
//              navigation sub-fields not surfaced by FormatMetadataExtensions.
// Architecture notes (ADR-038 P5):
//              IDE doc pane consumes these models. No UI code here — UI lives
//              in WpfHexEditor.App. Both casings (camelCase + PascalCase) and
//              both schemas (string-array vs object-array, dict vs array) are
//              accepted at read time.
//////////////////////////////////////////////

using System.Text.Json;
using WpfHexEditor.Core.Contracts;
using WpfHexEditor.Core.Definitions.Models;

namespace WpfHexEditor.Core.Definitions.Metadata;

// ---------------------------------------------------------------------------
// Documentary model types
// ---------------------------------------------------------------------------

/// <summary>A software entry — either a bare name or a structured record with url/role.</summary>
public sealed record SoftwareReference(string Name, string? Url, string? Role);

/// <summary>A relationship to another format (e.g. ELF → "ELF replaced a.out").</summary>
public sealed record FormatRelationship(string Format, string? Relationship);

/// <summary>A documentation reference — spec name or web link.</summary>
public sealed record DocReference(string Title, bool IsWebLink);

/// <summary>Inspector pane header info: badge variable, primary highlight field, quality score toggle.</summary>
public sealed record InspectorHeader(string? Badge, string? PrimaryField, bool ShowQualityScore);

/// <summary>Navigation overview: declared entry point, ordered structure, free-form notes.</summary>
public sealed record NavigationOverview(
    string? EntryPoint,
    IReadOnlyList<string> Structure,
    string? Notes);

// ---------------------------------------------------------------------------
// Extension methods
// ---------------------------------------------------------------------------

/// <summary>
/// Documentation-pane extensions on <see cref="EmbeddedFormatEntry"/>.
/// Surfaces fields that the runtime declared but did not read until P5.
/// </summary>
public static class FormatDocumentationExtensions
{
    // ----- Software -----------------------------------------------------------

    /// <summary>
    /// Returns the <c>software</c> / <c>Software</c> entries. Accepts both schemas:
    /// string array (most files) or object array with <c>name/url/role</c> (e.g. A_OUT).
    /// </summary>
    public static IReadOnlyList<SoftwareReference> GetSoftware(
        this EmbeddedFormatEntry entry, IEmbeddedFormatCatalog catalog)
    {
        using var doc = JsonDocument.Parse(catalog.GetJson(entry.ResourceKey), WhfmtJsonOptions.Jsonc);
        var root = doc.RootElement;
        // Prefer camelCase ("software") then fall back to PascalCase ("Software").
        if (TryGetArray(root, "software", out var arr) || TryGetArray(root, "Software", out arr))
            return ReadSoftwareArray(arr);
        return [];
    }

    private static IReadOnlyList<SoftwareReference> ReadSoftwareArray(JsonElement arr)
    {
        var list = new List<SoftwareReference>(arr.GetArrayLength());
        foreach (var item in arr.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                if (item.GetString() is { Length: > 0 } s)
                    list.Add(new SoftwareReference(s, null, null));
            }
            else if (item.ValueKind == JsonValueKind.Object)
            {
                var name = Str(item, "name");
                if (!string.IsNullOrEmpty(name))
                    list.Add(new SoftwareReference(name, StrN(item, "url"), StrN(item, "role")));
            }
        }
        return list;
    }

    // ----- UseCases -----------------------------------------------------------

    /// <summary>Returns the <c>useCases</c> / <c>UseCases</c> string array.</summary>
    public static IReadOnlyList<string> GetUseCases(
        this EmbeddedFormatEntry entry, IEmbeddedFormatCatalog catalog)
    {
        using var doc = JsonDocument.Parse(catalog.GetJson(entry.ResourceKey), WhfmtJsonOptions.Jsonc);
        var root = doc.RootElement;
        if (TryGetArray(root, "useCases", out var arr) || TryGetArray(root, "UseCases", out arr))
            return ReadStringArray(arr);
        return [];
    }

    // ----- References ---------------------------------------------------------

    /// <summary>
    /// Returns the <c>references</c> entries, normalising the two competing schemas:
    /// string array OR named-object (<c>{ specifications: [], webLinks: [] }</c>).
    /// </summary>
    public static IReadOnlyList<DocReference> GetReferences(
        this EmbeddedFormatEntry entry, IEmbeddedFormatCatalog catalog)
    {
        using var doc = JsonDocument.Parse(catalog.GetJson(entry.ResourceKey), WhfmtJsonOptions.Jsonc);
        var root = doc.RootElement;
        if (!root.TryGetProperty("references", out var refs)) return [];

        var list = new List<DocReference>();
        if (refs.ValueKind == JsonValueKind.Array)
        {
            foreach (var s in ReadStringArray(refs))
                list.Add(new DocReference(s, LooksLikeUrl(s)));
        }
        else if (refs.ValueKind == JsonValueKind.Object)
        {
            // Each property is a named bucket; classify by name when possible.
            foreach (var prop in refs.EnumerateObject())
            {
                if (prop.Value.ValueKind != JsonValueKind.Array) continue;
                var bucketIsLinks = prop.Name.Equals("webLinks", StringComparison.OrdinalIgnoreCase)
                                 || prop.Name.Equals("WebLinks", StringComparison.OrdinalIgnoreCase)
                                 || prop.Name.Equals("urls",     StringComparison.OrdinalIgnoreCase);
                foreach (var s in ReadStringArray(prop.Value))
                    list.Add(new DocReference(s, bucketIsLinks || LooksLikeUrl(s)));
            }
        }
        return list;
    }

    // ----- formatRelationships ------------------------------------------------

    /// <summary>
    /// Returns <c>formatRelationships</c> entries. Accepts both schemas:
    /// array of <c>{format, relationship}</c> objects (A_OUT style) OR a dict
    /// where keys are relationship types and values are format names or arrays.
    /// </summary>
    public static IReadOnlyList<FormatRelationship> GetFormatRelationships(
        this EmbeddedFormatEntry entry, IEmbeddedFormatCatalog catalog)
    {
        using var doc = JsonDocument.Parse(catalog.GetJson(entry.ResourceKey), WhfmtJsonOptions.Jsonc);
        var root = doc.RootElement;
        if (!root.TryGetProperty("formatRelationships", out var rel)) return [];

        var list = new List<FormatRelationship>();
        if (rel.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in rel.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                var fmt = Str(item, "format");
                if (!string.IsNullOrEmpty(fmt))
                    list.Add(new FormatRelationship(fmt, StrN(item, "relationship")));
            }
        }
        else if (rel.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in rel.EnumerateObject())
            {
                // Skip pure-metadata keys that aren't relationships
                if (prop.Name.Equals("category", StringComparison.OrdinalIgnoreCase)) continue;
                if (prop.Name.Equals("extensions", StringComparison.OrdinalIgnoreCase)) continue;

                if (prop.Value.ValueKind == JsonValueKind.String)
                    list.Add(new FormatRelationship(prop.Value.GetString() ?? "", prop.Name));
                else if (prop.Value.ValueKind == JsonValueKind.Array)
                    foreach (var s in ReadStringArray(prop.Value))
                        list.Add(new FormatRelationship(s, prop.Name));
            }
        }
        return list;
    }

    // ----- Inspector header ---------------------------------------------------

    /// <summary>
    /// Returns the inspector pane header info (badge / primaryField / showQualityScore).
    /// Returns null when no <c>inspector</c> block is present.
    /// </summary>
    public static InspectorHeader? GetInspectorHeader(
        this EmbeddedFormatEntry entry, IEmbeddedFormatCatalog catalog)
    {
        using var doc = JsonDocument.Parse(catalog.GetJson(entry.ResourceKey), WhfmtJsonOptions.Jsonc);
        return ReadInspectorHeader(doc.RootElement);
    }

    // ----- Navigation overview ------------------------------------------------

    /// <summary>
    /// Returns the navigation overview (entryPoint / structure[] / notes) — distinct
    /// from <c>navigation.bookmarks[]</c> already exposed by FormatMetadataExtensions.
    /// </summary>
    public static NavigationOverview? GetNavigationOverview(
        this EmbeddedFormatEntry entry, IEmbeddedFormatCatalog catalog)
    {
        using var doc = JsonDocument.Parse(catalog.GetJson(entry.ResourceKey), WhfmtJsonOptions.Jsonc);
        return ReadNavigationOverview(doc.RootElement);
    }

    // ----- Forensic notes (A_OUT-style free-form string) ----------------------

    /// <summary>
    /// Returns the <c>forensic.notes</c> string when present. Distinct from the
    /// structured <c>suspiciousPatterns[]</c> exposed by FormatMetadataExtensions.
    /// </summary>
    public static string? GetForensicNotes(
        this EmbeddedFormatEntry entry, IEmbeddedFormatCatalog catalog)
    {
        using var doc = JsonDocument.Parse(catalog.GetJson(entry.ResourceKey), WhfmtJsonOptions.Jsonc);
        return ReadForensicNotes(doc.RootElement);
    }

    // ----- Single-parse bundle (callers needing 2+ of the above) --------------

    /// <summary>
    /// Returns inspector header + navigation overview + forensic notes in a single
    /// JSON parse. Use when a caller (e.g. the Parsed Fields panel) needs the
    /// whole documentary bundle for one format — three times faster than calling
    /// the individual <c>GetInspectorHeader</c> / <c>GetNavigationOverview</c> /
    /// <c>GetForensicNotes</c> methods, which would re-parse the JSON each call.
    /// </summary>
    public static (InspectorHeader? Inspector, NavigationOverview? Navigation, string? ForensicNotes)
        GetDocumentationBundle(this EmbeddedFormatEntry entry, IEmbeddedFormatCatalog catalog)
    {
        using var doc = JsonDocument.Parse(catalog.GetJson(entry.ResourceKey), WhfmtJsonOptions.Jsonc);
        var root = doc.RootElement;
        return (ReadInspectorHeader(root), ReadNavigationOverview(root), ReadForensicNotes(root));
    }

    private static InspectorHeader? ReadInspectorHeader(JsonElement root)
    {
        if (!root.TryGetProperty("inspector", out var ins) || ins.ValueKind != JsonValueKind.Object)
            return null;
        bool showQs = ins.TryGetProperty("showQualityScore", out var sqs) && sqs.ValueKind == JsonValueKind.True;
        return new InspectorHeader(StrN(ins, "badge"), StrN(ins, "primaryField"), showQs);
    }

    private static NavigationOverview? ReadNavigationOverview(JsonElement root)
    {
        if (!root.TryGetProperty("navigation", out var nav) || nav.ValueKind != JsonValueKind.Object)
            return null;
        var entryPoint = StrN(nav, "entryPoint");
        var notes      = StrN(nav, "notes");
        var structure  = nav.TryGetProperty("structure", out var st) && st.ValueKind == JsonValueKind.Array
            ? ReadStringArray(st)
            : (IReadOnlyList<string>)[];
        if (entryPoint is null && notes is null && structure.Count == 0) return null;
        return new NavigationOverview(entryPoint, structure, notes);
    }

    private static string? ReadForensicNotes(JsonElement root)
    {
        if (!root.TryGetProperty("forensic", out var f)) return null;
        return StrN(f, "notes");
    }

    // ----- Helpers ------------------------------------------------------------

    private static bool TryGetArray(JsonElement obj, string name, out JsonElement arr)
    {
        if (obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Array)
        {
            arr = v;
            return true;
        }
        arr = default;
        return false;
    }

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

    private static bool LooksLikeUrl(string s)
        => s.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
        || s.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
}
