//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Text.Json;
using WpfHexEditor.Core.Contracts;

namespace WpfHexEditor.Core.Definitions.Metadata;

// ---------------------------------------------------------------------------
// Lightweight model types
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

/// <summary>
/// All rich metadata blocks extracted from a single <c>.whfmt</c> JSON parse.
/// Returned by <see cref="FormatMetadataExtensions.GetAllMetadata"/>.
/// </summary>
public sealed record FormatMetadata(
    EmbeddedFormatEntry Entry,
    ForensicSummary? Forensic,
    AiHints? AiHints,
    IReadOnlyList<NavigationBookmark> Bookmarks,
    IReadOnlyList<AssertionRule> Assertions,
    IReadOnlyList<InspectorGroup> InspectorGroups,
    IReadOnlyList<ExportTemplate> ExportTemplates,
    TechnicalDetails? TechnicalDetails)
{
    /// <summary>Shortcut — true when forensic risk is high or critical.</summary>
    public bool IsHighRisk => Forensic?.IsHighRisk == true;

    /// <summary>Shortcut — true when encryption is declared in technical details.</summary>
    public bool SupportsEncryption =>
        TechnicalDetails?.SupportsEncryption == true ||
        !string.IsNullOrWhiteSpace(TechnicalDetails?.Encryption);
}

// ---------------------------------------------------------------------------
// Extension methods
// ---------------------------------------------------------------------------

/// <summary>
/// Extension methods on <see cref="EmbeddedFormatEntry"/> that surface rich metadata
/// from the full <c>.whfmt</c> JSON without requiring the caller to parse JSON manually.
/// <para>
/// For multiple metadata blocks on the same entry, prefer
/// <see cref="GetAllMetadata"/> — it parses the JSON exactly once.
/// </para>
/// </summary>
public static class FormatMetadataExtensions
{
    private static readonly JsonDocumentOptions s_opts = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    // ------------------------------------------------------------------
    // Bulk — single parse, all blocks
    // ------------------------------------------------------------------

    /// <summary>
    /// Parses the <c>.whfmt</c> JSON exactly once and returns all rich metadata blocks.
    /// Use this when you need two or more metadata blocks for the same entry —
    /// it is significantly more efficient than calling individual methods in sequence.
    /// </summary>
    public static FormatMetadata GetAllMetadata(
        this EmbeddedFormatEntry entry, IEmbeddedFormatCatalog catalog)
    {
        using var doc = JsonDocument.Parse(catalog.GetJson(entry.ResourceKey), s_opts);
        var root = doc.RootElement;

        return new FormatMetadata(
            Entry:           entry,
            Forensic:        ParseForensic(root),
            AiHints:         ParseAiHints(root),
            Bookmarks:       ParseBookmarks(root),
            Assertions:      ParseAssertions(root),
            InspectorGroups: ParseInspectorGroups(root),
            ExportTemplates: ParseExportTemplates(root),
            TechnicalDetails: ParseTechnicalDetails(root));
    }

    // ------------------------------------------------------------------
    // Forensic
    // ------------------------------------------------------------------

    /// <summary>
    /// Extracts the <c>forensic</c> block. Returns <see langword="null"/> when absent.
    /// </summary>
    public static ForensicSummary? GetForensicSummary(
        this EmbeddedFormatEntry entry, IEmbeddedFormatCatalog catalog)
    {
        using var doc = JsonDocument.Parse(catalog.GetJson(entry.ResourceKey), s_opts);
        return ParseForensic(doc.RootElement);
    }

    /// <summary>True when the format's forensic risk level is <c>"high"</c> or <c>"critical"</c>.</summary>
    public static bool IsHighRisk(this EmbeddedFormatEntry entry, IEmbeddedFormatCatalog catalog)
        => entry.GetForensicSummary(catalog)?.IsHighRisk == true;

    // ------------------------------------------------------------------
    // AI Hints
    // ------------------------------------------------------------------

    /// <summary>
    /// Extracts the <c>aiHints</c> block. Returns <see langword="null"/> when absent.
    /// </summary>
    public static AiHints? GetAiHints(
        this EmbeddedFormatEntry entry, IEmbeddedFormatCatalog catalog)
    {
        using var doc = JsonDocument.Parse(catalog.GetJson(entry.ResourceKey), s_opts);
        return ParseAiHints(doc.RootElement);
    }

    // ------------------------------------------------------------------
    // Navigation
    // ------------------------------------------------------------------

    /// <summary>
    /// Returns all navigation bookmarks from <c>navigation.bookmarks</c>.
    /// Returns an empty list when the block is absent.
    /// </summary>
    public static IReadOnlyList<NavigationBookmark> GetNavigationBookmarks(
        this EmbeddedFormatEntry entry, IEmbeddedFormatCatalog catalog)
    {
        using var doc = JsonDocument.Parse(catalog.GetJson(entry.ResourceKey), s_opts);
        return ParseBookmarks(doc.RootElement);
    }

    // ------------------------------------------------------------------
    // Assertions
    // ------------------------------------------------------------------

    /// <summary>
    /// Returns all assertion rules from the <c>assertions</c> array.
    /// Returns an empty list when the block is absent.
    /// </summary>
    public static IReadOnlyList<AssertionRule> GetAssertions(
        this EmbeddedFormatEntry entry, IEmbeddedFormatCatalog catalog)
    {
        using var doc = JsonDocument.Parse(catalog.GetJson(entry.ResourceKey), s_opts);
        return ParseAssertions(doc.RootElement);
    }

    // ------------------------------------------------------------------
    // Inspector
    // ------------------------------------------------------------------

    /// <summary>
    /// Returns all inspector groups from <c>inspector.groups</c>.
    /// Returns an empty list when the block is absent.
    /// </summary>
    public static IReadOnlyList<InspectorGroup> GetInspectorGroups(
        this EmbeddedFormatEntry entry, IEmbeddedFormatCatalog catalog)
    {
        using var doc = JsonDocument.Parse(catalog.GetJson(entry.ResourceKey), s_opts);
        return ParseInspectorGroups(doc.RootElement);
    }

    // ------------------------------------------------------------------
    // Export templates
    // ------------------------------------------------------------------

    /// <summary>
    /// Returns all export templates from the <c>exportTemplates</c> array.
    /// Returns an empty list when the block is absent.
    /// </summary>
    public static IReadOnlyList<ExportTemplate> GetExportTemplates(
        this EmbeddedFormatEntry entry, IEmbeddedFormatCatalog catalog)
    {
        using var doc = JsonDocument.Parse(catalog.GetJson(entry.ResourceKey), s_opts);
        return ParseExportTemplates(doc.RootElement);
    }

    // ------------------------------------------------------------------
    // Technical details
    // ------------------------------------------------------------------

    /// <summary>
    /// Extracts the <c>TechnicalDetails</c> block.
    /// Returns <see langword="null"/> when the block is absent or empty.
    /// </summary>
    public static TechnicalDetails? GetTechnicalDetails(
        this EmbeddedFormatEntry entry, IEmbeddedFormatCatalog catalog)
    {
        using var doc = JsonDocument.Parse(catalog.GetJson(entry.ResourceKey), s_opts);
        return ParseTechnicalDetails(doc.RootElement);
    }

    /// <summary>
    /// True when the format declares encryption support in <c>TechnicalDetails</c>.
    /// </summary>
    public static bool SupportsEncryption(this EmbeddedFormatEntry entry, IEmbeddedFormatCatalog catalog)
    {
        var td = entry.GetTechnicalDetails(catalog);
        return td?.SupportsEncryption == true || !string.IsNullOrWhiteSpace(td?.Encryption);
    }

    // ------------------------------------------------------------------
    // Internal parsers — operate on a already-open JsonElement
    // (called by both GetAllMetadata and the individual public methods)
    // ------------------------------------------------------------------

    internal static ForensicSummary? ParseForensic(JsonElement root)
    {
        if (!root.TryGetProperty("forensic", out var f)) return null;
        return new ForensicSummary(
            Category:          Str(f, "category"),
            RiskLevel:         Str(f, "riskLevel"),
            SuspiciousPatterns: ReadSuspiciousPatterns(f),
            MaliciousPatterns:  ReadMaliciousPatterns(f));
    }

    internal static AiHints? ParseAiHints(JsonElement root)
    {
        if (!root.TryGetProperty("aiHints", out var ai)) return null;
        return new AiHints(
            AnalysisContext:     ai.TryGetProperty("analysisContext",    out var ac) ? ac.GetString() : null,
            SuggestedInspections: ai.TryGetProperty("suggestedInspections", out var si) ? ReadStringArray(si) : [],
            KnownVulnerabilities: ai.TryGetProperty("knownVulnerabilities", out var kv) ? ReadStringArray(kv) : []);
    }

    internal static IReadOnlyList<NavigationBookmark> ParseBookmarks(JsonElement root)
    {
        if (!root.TryGetProperty("navigation", out var nav)) return [];
        if (!nav.TryGetProperty("bookmarks", out var bm) || bm.ValueKind != JsonValueKind.Array) return [];

        var list = new List<NavigationBookmark>(bm.GetArrayLength());
        foreach (var item in bm.EnumerateArray())
        {
            int? offset = item.TryGetProperty("offset", out var ov) && ov.TryGetInt32(out var oi) ? oi : null;
            list.Add(new NavigationBookmark(Str(item, "name"), offset, StrN(item, "offsetVar"), StrN(item, "icon")));
        }
        return list;
    }

    internal static IReadOnlyList<AssertionRule> ParseAssertions(JsonElement root)
    {
        if (!root.TryGetProperty("assertions", out var arr) || arr.ValueKind != JsonValueKind.Array) return [];
        var list = new List<AssertionRule>(arr.GetArrayLength());
        foreach (var item in arr.EnumerateArray())
            list.Add(new AssertionRule(Str(item, "name"), Str(item, "expression"), Str(item, "severity"), StrN(item, "message")));
        return list;
    }

    internal static IReadOnlyList<InspectorGroup> ParseInspectorGroups(JsonElement root)
    {
        if (!root.TryGetProperty("inspector", out var ins)) return [];
        if (!ins.TryGetProperty("groups", out var groups) || groups.ValueKind != JsonValueKind.Array) return [];
        var list = new List<InspectorGroup>(groups.GetArrayLength());
        foreach (var g in groups.EnumerateArray())
            list.Add(new InspectorGroup(Str(g, "title"), StrN(g, "icon"),
                g.TryGetProperty("fields", out var f) ? ReadStringArray(f) : []));
        return list;
    }

    internal static IReadOnlyList<ExportTemplate> ParseExportTemplates(JsonElement root)
    {
        if (!root.TryGetProperty("exportTemplates", out var arr) || arr.ValueKind != JsonValueKind.Array) return [];
        var list = new List<ExportTemplate>(arr.GetArrayLength());
        foreach (var t in arr.EnumerateArray())
            list.Add(new ExportTemplate(Str(t, "name"), Str(t, "format"),
                t.TryGetProperty("fields", out var f) ? ReadStringArray(f) : []));
        return list;
    }

    internal static TechnicalDetails? ParseTechnicalDetails(JsonElement root)
    {
        if (!root.TryGetProperty("TechnicalDetails", out var td)) return null;
        bool? supportsEncryption = td.TryGetProperty("supportsEncryption", out var se)
            ? se.ValueKind == JsonValueKind.True : null;
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
        var list = new List<string>(el.GetArrayLength());
        foreach (var item in el.EnumerateArray())
            if (item.GetString() is { } s) list.Add(s);
        return list;
    }

    private static IReadOnlyList<SuspiciousPattern> ReadSuspiciousPatterns(JsonElement forensic)
    {
        if (!forensic.TryGetProperty("suspiciousPatterns", out var arr) || arr.ValueKind != JsonValueKind.Array) return [];
        var list = new List<SuspiciousPattern>(arr.GetArrayLength());
        foreach (var p in arr.EnumerateArray())
            list.Add(new SuspiciousPattern(Str(p, "name"), Str(p, "description"), StrN(p, "condition")));
        return list;
    }

    private static IReadOnlyList<MaliciousPattern> ReadMaliciousPatterns(JsonElement forensic)
    {
        if (!forensic.TryGetProperty("knownMaliciousPatterns", out var arr) || arr.ValueKind != JsonValueKind.Array) return [];
        var list = new List<MaliciousPattern>(arr.GetArrayLength());
        foreach (var p in arr.EnumerateArray())
            list.Add(new MaliciousPattern(Str(p, "name"), Str(p, "description")));
        return list;
    }
}
