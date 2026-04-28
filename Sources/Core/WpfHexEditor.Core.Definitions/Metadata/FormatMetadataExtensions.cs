//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Text.Json;
using WpfHexEditor.Core.Contracts;

namespace WpfHexEditor.Core.Definitions.Metadata;

// ---------------------------------------------------------------------------
// Lightweight model types (no System.Text.Json source-gen needed —
// parsed on-demand from the already-cached JSON text)
// ---------------------------------------------------------------------------

/// <summary>A suspicious pattern declared in a format's <c>forensic</c> block.</summary>
public sealed record SuspiciousPattern(string Name, string Description, string? Condition);

/// <summary>A known malicious pattern declared in a format's <c>forensic</c> block.</summary>
public sealed record MaliciousPattern(string Name, string Description);

/// <summary>Forensic metadata extracted from a <c>.whfmt</c> file's <c>forensic</c> block.</summary>
public sealed record ForensicSummary(
    string Category,
    string RiskLevel,
    IReadOnlyList<SuspiciousPattern> SuspiciousPatterns,
    IReadOnlyList<MaliciousPattern> MaliciousPatterns)
{
    /// <summary>True when <see cref="RiskLevel"/> is <c>"high"</c> or <c>"critical"</c>.</summary>
    public bool IsHighRisk =>
        RiskLevel.Equals("high", StringComparison.OrdinalIgnoreCase) ||
        RiskLevel.Equals("critical", StringComparison.OrdinalIgnoreCase);
}

/// <summary>A navigation bookmark declared in a format's <c>navigation</c> block.</summary>
public sealed record NavigationBookmark(string Name, int? Offset, string? OffsetVar, string? Icon);

/// <summary>An assertion rule declared in a format's <c>assertions</c> block.</summary>
public sealed record AssertionRule(string Name, string Expression, string Severity, string? Message);

/// <summary>An inspector group declared in a format's <c>inspector</c> block.</summary>
public sealed record InspectorGroup(string Title, string? Icon, IReadOnlyList<string> Fields);

/// <summary>An export template declared in a format's <c>exportTemplates</c> block.</summary>
public sealed record ExportTemplate(string Name, string Format, IReadOnlyList<string> Fields);

/// <summary>AI analysis hints extracted from a format's <c>aiHints</c> block.</summary>
public sealed record AiHints(
    string? AnalysisContext,
    IReadOnlyList<string> SuggestedInspections,
    IReadOnlyList<string> KnownVulnerabilities);

/// <summary>Technical metadata extracted from a format's <c>TechnicalDetails</c> block.</summary>
public sealed record TechnicalDetails(
    string? Endianness,
    string? CompressionMethod,
    string? Platform,
    string? Encryption,
    bool? SupportsEncryption,
    string? BitDepth,
    string? ColorSpace,
    string? SampleRate,
    string? Container,
    string? DataStructure);

// ---------------------------------------------------------------------------
// Extension methods
// ---------------------------------------------------------------------------

/// <summary>
/// Extension methods on <see cref="EmbeddedFormatEntry"/> that surface rich metadata
/// from the full <c>.whfmt</c> JSON without requiring the caller to parse JSON manually.
/// </summary>
public static class FormatMetadataExtensions
{
    private static readonly JsonDocumentOptions s_opts = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    // ------------------------------------------------------------------
    // Forensic
    // ------------------------------------------------------------------

    /// <summary>
    /// Extracts the <c>forensic</c> block from the format definition.
    /// Returns <see langword="null"/> when the block is absent.
    /// </summary>
    public static ForensicSummary? GetForensicSummary(
        this EmbeddedFormatEntry entry, IEmbeddedFormatCatalog catalog)
    {
        using var doc = JsonDocument.Parse(catalog.GetJson(entry.ResourceKey), s_opts);
        if (!doc.RootElement.TryGetProperty("forensic", out var f)) return null;

        var category  = f.TryGetProperty("category",  out var c) ? c.GetString() ?? "" : "";
        var riskLevel = f.TryGetProperty("riskLevel", out var r) ? r.GetString() ?? "" : "";

        var suspicious = ReadSuspiciousPatterns(f);
        var malicious  = ReadMaliciousPatterns(f);

        return new ForensicSummary(category, riskLevel, suspicious, malicious);
    }

    /// <summary>
    /// Returns <see langword="true"/> when the format's forensic risk level is
    /// <c>"high"</c> or <c>"critical"</c>.
    /// </summary>
    public static bool IsHighRisk(this EmbeddedFormatEntry entry, IEmbeddedFormatCatalog catalog)
        => entry.GetForensicSummary(catalog)?.IsHighRisk == true;

    // ------------------------------------------------------------------
    // AI Hints
    // ------------------------------------------------------------------

    /// <summary>
    /// Extracts the <c>aiHints</c> block from the format definition.
    /// Returns <see langword="null"/> when the block is absent.
    /// </summary>
    public static AiHints? GetAiHints(
        this EmbeddedFormatEntry entry, IEmbeddedFormatCatalog catalog)
    {
        using var doc = JsonDocument.Parse(catalog.GetJson(entry.ResourceKey), s_opts);
        if (!doc.RootElement.TryGetProperty("aiHints", out var ai)) return null;

        var context = ai.TryGetProperty("analysisContext", out var ac) ? ac.GetString() : null;

        var inspections = ai.TryGetProperty("suggestedInspections", out var si)
            ? ReadStringArray(si) : [];

        var vulns = ai.TryGetProperty("knownVulnerabilities", out var kv)
            ? ReadStringArray(kv) : [];

        return new AiHints(context, inspections, vulns);
    }

    // ------------------------------------------------------------------
    // Navigation
    // ------------------------------------------------------------------

    /// <summary>
    /// Returns all navigation bookmarks declared in the format's <c>navigation.bookmarks</c> array.
    /// Returns an empty list when the block is absent.
    /// </summary>
    public static IReadOnlyList<NavigationBookmark> GetNavigationBookmarks(
        this EmbeddedFormatEntry entry, IEmbeddedFormatCatalog catalog)
    {
        using var doc = JsonDocument.Parse(catalog.GetJson(entry.ResourceKey), s_opts);
        if (!doc.RootElement.TryGetProperty("navigation", out var nav)) return [];
        if (!nav.TryGetProperty("bookmarks", out var bm) || bm.ValueKind != JsonValueKind.Array)
            return [];

        var list = new List<NavigationBookmark>();
        foreach (var item in bm.EnumerateArray())
        {
            var name      = Str(item, "name");
            var icon      = StrN(item, "icon");
            var offsetVar = StrN(item, "offsetVar");
            int? offset   = item.TryGetProperty("offset", out var ov) && ov.TryGetInt32(out var oi)
                ? oi : null;
            list.Add(new NavigationBookmark(name, offset, offsetVar, icon));
        }
        return list;
    }

    // ------------------------------------------------------------------
    // Assertions
    // ------------------------------------------------------------------

    /// <summary>
    /// Returns all assertion rules declared in the format's <c>assertions</c> array.
    /// Returns an empty list when the block is absent.
    /// </summary>
    public static IReadOnlyList<AssertionRule> GetAssertions(
        this EmbeddedFormatEntry entry, IEmbeddedFormatCatalog catalog)
    {
        using var doc = JsonDocument.Parse(catalog.GetJson(entry.ResourceKey), s_opts);
        if (!doc.RootElement.TryGetProperty("assertions", out var arr)
            || arr.ValueKind != JsonValueKind.Array) return [];

        var list = new List<AssertionRule>();
        foreach (var item in arr.EnumerateArray())
            list.Add(new AssertionRule(
                Str(item, "name"),
                Str(item, "expression"),
                Str(item, "severity"),
                StrN(item, "message")));
        return list;
    }

    // ------------------------------------------------------------------
    // Inspector
    // ------------------------------------------------------------------

    /// <summary>
    /// Returns all inspector groups declared in the format's <c>inspector.groups</c> array.
    /// Returns an empty list when the block is absent.
    /// </summary>
    public static IReadOnlyList<InspectorGroup> GetInspectorGroups(
        this EmbeddedFormatEntry entry, IEmbeddedFormatCatalog catalog)
    {
        using var doc = JsonDocument.Parse(catalog.GetJson(entry.ResourceKey), s_opts);
        if (!doc.RootElement.TryGetProperty("inspector", out var ins)) return [];
        if (!ins.TryGetProperty("groups", out var groups) || groups.ValueKind != JsonValueKind.Array)
            return [];

        var list = new List<InspectorGroup>();
        foreach (var g in groups.EnumerateArray())
        {
            var title  = Str(g, "title");
            var icon   = StrN(g, "icon");
            var fields = g.TryGetProperty("fields", out var f) ? ReadStringArray(f) : [];
            list.Add(new InspectorGroup(title, icon, fields));
        }
        return list;
    }

    // ------------------------------------------------------------------
    // Export templates
    // ------------------------------------------------------------------

    /// <summary>
    /// Returns all export templates declared in the format's <c>exportTemplates</c> array.
    /// Returns an empty list when the block is absent.
    /// </summary>
    public static IReadOnlyList<ExportTemplate> GetExportTemplates(
        this EmbeddedFormatEntry entry, IEmbeddedFormatCatalog catalog)
    {
        using var doc = JsonDocument.Parse(catalog.GetJson(entry.ResourceKey), s_opts);
        if (!doc.RootElement.TryGetProperty("exportTemplates", out var arr)
            || arr.ValueKind != JsonValueKind.Array) return [];

        var list = new List<ExportTemplate>();
        foreach (var t in arr.EnumerateArray())
        {
            var name   = Str(t, "name");
            var format = Str(t, "format");
            var fields = t.TryGetProperty("fields", out var f) ? ReadStringArray(f) : [];
            list.Add(new ExportTemplate(name, format, fields));
        }
        return list;
    }

    // ------------------------------------------------------------------
    // Technical details
    // ------------------------------------------------------------------

    /// <summary>
    /// Extracts the <c>TechnicalDetails</c> block from the format definition.
    /// Returns <see langword="null"/> when the block is absent or empty.
    /// </summary>
    public static TechnicalDetails? GetTechnicalDetails(
        this EmbeddedFormatEntry entry, IEmbeddedFormatCatalog catalog)
    {
        using var doc = JsonDocument.Parse(catalog.GetJson(entry.ResourceKey), s_opts);
        if (!doc.RootElement.TryGetProperty("TechnicalDetails", out var td)) return null;

        bool? supportsEncryption = null;
        if (td.TryGetProperty("supportsEncryption", out var se))
            supportsEncryption = se.ValueKind == JsonValueKind.True;

        return new TechnicalDetails(
            Endianness:         StrN(td, "endianness"),
            CompressionMethod:  StrN(td, "compressionMethod"),
            Platform:           StrN(td, "Platform"),
            Encryption:         StrN(td, "encryption"),
            SupportsEncryption: supportsEncryption,
            BitDepth:           StrN(td, "bitDepth"),
            ColorSpace:         StrN(td, "colorSpace"),
            SampleRate:         StrN(td, "sampleRate"),
            Container:          StrN(td, "container"),
            DataStructure:      StrN(td, "dataStructure"));
    }

    /// <summary>
    /// Returns <see langword="true"/> when the format declares encryption support
    /// in its <c>TechnicalDetails</c> block.
    /// </summary>
    public static bool SupportsEncryption(this EmbeddedFormatEntry entry, IEmbeddedFormatCatalog catalog)
    {
        var td = entry.GetTechnicalDetails(catalog);
        return td?.SupportsEncryption == true ||
               !string.IsNullOrWhiteSpace(td?.Encryption);
    }

    // ------------------------------------------------------------------
    // Private helpers
    // ------------------------------------------------------------------

    private static string Str(JsonElement el, string prop)
        => el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? "" : "";

    private static string? StrN(JsonElement el, string prop)
        => el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() : null;

    private static IReadOnlyList<string> ReadStringArray(JsonElement el)
    {
        if (el.ValueKind != JsonValueKind.Array) return [];
        var list = new List<string>();
        foreach (var item in el.EnumerateArray())
            if (item.GetString() is { } s) list.Add(s);
        return list;
    }

    private static IReadOnlyList<SuspiciousPattern> ReadSuspiciousPatterns(JsonElement forensic)
    {
        if (!forensic.TryGetProperty("suspiciousPatterns", out var arr)
            || arr.ValueKind != JsonValueKind.Array) return [];
        var list = new List<SuspiciousPattern>();
        foreach (var p in arr.EnumerateArray())
            list.Add(new SuspiciousPattern(Str(p, "name"), Str(p, "description"), StrN(p, "condition")));
        return list;
    }

    private static IReadOnlyList<MaliciousPattern> ReadMaliciousPatterns(JsonElement forensic)
    {
        if (!forensic.TryGetProperty("knownMaliciousPatterns", out var arr)
            || arr.ValueKind != JsonValueKind.Array) return [];
        var list = new List<MaliciousPattern>();
        foreach (var p in arr.EnumerateArray())
            list.Add(new MaliciousPattern(Str(p, "name"), Str(p, "description")));
        return list;
    }
}
