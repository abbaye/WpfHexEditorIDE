//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Text;
using WpfHexEditor.Core.Contracts;

namespace WpfHexEditor.Core.Definitions.Metadata;

/// <summary>
/// Generates human-readable summaries of <see cref="EmbeddedFormatEntry"/> objects
/// without any dependency on WPF or MVVM infrastructure.
/// <para>
/// Methods that accept a catalog parameter parse the <c>.whfmt</c> JSON
/// exactly once via <see cref="FormatMetadataExtensions.GetAllMetadata"/> and pass
/// the resulting <see cref="FormatMetadata"/> to the rendering helpers —
/// no redundant I/O or repeated JSON parsing.
/// </para>
/// </summary>
public static class FormatSummaryBuilder
{
    // ------------------------------------------------------------------
    // Plain text
    // ------------------------------------------------------------------

    /// <summary>
    /// Returns a single-line description suitable for status bars or tooltips.
    /// <example><c>ZIP Archive (Archives) — .zip .jar .apk — Quality: 92%</c></example>
    /// </summary>
    public static string BuildOneLiner(EmbeddedFormatEntry entry)
    {
        var exts = entry.Extensions.Count > 0
            ? string.Join(" ", entry.Extensions)
            : "(no extension)";
        return $"{entry.Name} ({entry.Category}) — {exts} — Quality: {entry.QualityScore}%";
    }

    /// <summary>
    /// Returns a multi-line plain-text summary.
    /// When <paramref name="catalog"/> is provided the JSON is parsed once and all
    /// available rich metadata blocks are appended.
    /// </summary>
    public static string BuildPlainText(EmbeddedFormatEntry entry, IEmbeddedFormatCatalog? catalog = null)
    {
        var sb = new StringBuilder();
        AppendHeader(sb, entry);

        if (catalog is not null)
            AppendRichPlainText(sb, entry.GetAllMetadata(catalog));

        return sb.ToString();
    }

    // ------------------------------------------------------------------
    // Markdown
    // ------------------------------------------------------------------

    /// <summary>
    /// Returns a Markdown-formatted summary card for the format.
    /// When <paramref name="catalog"/> is provided the JSON is parsed once and all
    /// available rich metadata blocks are appended.
    /// </summary>
    public static string BuildMarkdown(EmbeddedFormatEntry entry, IEmbeddedFormatCatalog? catalog = null)
    {
        var sb = new StringBuilder();
        AppendMarkdownHeader(sb, entry);

        if (catalog is not null)
            AppendRichMarkdown(sb, entry.GetAllMetadata(catalog));

        return sb.ToString();
    }

    // ------------------------------------------------------------------
    // Diagnostic dump
    // ------------------------------------------------------------------

    /// <summary>
    /// Returns a verbose diagnostic dump for debugging.
    /// When <paramref name="catalog"/> is provided the JSON is parsed exactly once
    /// and all metadata blocks are included.
    /// </summary>
    public static string BuildDiagnosticDump(EmbeddedFormatEntry entry, IEmbeddedFormatCatalog? catalog = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== FORMAT DIAGNOSTIC DUMP ===");
        sb.AppendLine($"ResourceKey         : {entry.ResourceKey}");
        sb.AppendLine($"Name                : {entry.Name}");
        sb.AppendLine($"Category            : {entry.Category}");
        sb.AppendLine($"Description         : {entry.Description}");
        sb.AppendLine($"Version             : {entry.Version}");
        sb.AppendLine($"Author              : {entry.Author}");
        sb.AppendLine($"Platform            : {entry.Platform}");
        sb.AppendLine($"QualityScore        : {entry.QualityScore}");
        sb.AppendLine($"IsTextFormat        : {entry.IsTextFormat}");
        sb.AppendLine($"HasSyntaxDefinition : {entry.HasSyntaxDefinition}");
        sb.AppendLine($"PreferredEditor     : {entry.PreferredEditor ?? "(null)"}");
        sb.AppendLine($"DiffMode            : {entry.DiffMode ?? "(null)"}");
        sb.AppendLine($"Extensions          : [{string.Join(", ", entry.Extensions)}]");
        sb.AppendLine($"MimeTypes           : [{string.Join(", ", entry.MimeTypes ?? [])}]");

        if (entry.Signatures is { Count: > 0 })
        {
            sb.AppendLine($"Signatures ({entry.Signatures.Count}):");
            foreach (var sig in entry.Signatures)
                sb.AppendLine($"  [{sig.Offset,4}] {FormatHex(sig.Value)}  w={sig.Weight:F2}");
        }
        else
        {
            sb.AppendLine("Signatures          : (none)");
        }

        if (catalog is not null)
        {
            // Single JSON parse for all blocks
            var meta = entry.GetAllMetadata(catalog);

            if (meta.Forensic is { } f)
            {
                sb.AppendLine($"Forensic.Category   : {f.Category}");
                sb.AppendLine($"Forensic.RiskLevel  : {f.RiskLevel}  (high={f.IsHighRisk})");
                foreach (var p in f.SuspiciousPatterns)
                    sb.AppendLine($"  SUSPICIOUS: {p.Name} — {p.Description}");
            }

            if (meta.Assertions.Count > 0)
            {
                sb.AppendLine($"Assertions ({meta.Assertions.Count}):");
                foreach (var a in meta.Assertions)
                    sb.AppendLine($"  [{a.Severity.ToUpperInvariant()}] {a.Name}: {a.Expression}");
            }

            if (meta.Bookmarks.Count > 0)
            {
                sb.AppendLine($"Bookmarks ({meta.Bookmarks.Count}):");
                foreach (var b in meta.Bookmarks)
                    sb.AppendLine($"  {b.Name}  offset={b.Offset?.ToString() ?? b.OffsetVar ?? "?"}  icon={b.Icon}");
            }

            foreach (var e in meta.ExportTemplates)
                sb.AppendLine($"ExportTemplate: {e.Name} [{e.Format}]  fields=[{string.Join(", ", e.Fields)}]");

            if (meta.TechnicalDetails is { } td)
            {
                sb.AppendLine("TechnicalDetails:");
                if (td.Endianness        is not null) sb.AppendLine($"  Endianness        : {td.Endianness}");
                if (td.CompressionMethod is not null) sb.AppendLine($"  Compression       : {td.CompressionMethod}");
                if (td.Encryption        is not null) sb.AppendLine($"  Encryption        : {td.Encryption}");
                if (td.SupportsEncryption is not null) sb.AppendLine($"  SupportsEncryption: {td.SupportsEncryption}");
                if (td.DataStructure     is not null) sb.AppendLine($"  DataStructure     : {td.DataStructure}");
            }
        }

        sb.AppendLine("==============================");
        return sb.ToString();
    }

    // ------------------------------------------------------------------
    // Signature helper
    // ------------------------------------------------------------------

    /// <summary>
    /// Formats a raw hex signature string with spaces between byte pairs.
    /// <example><c>"504B0304"</c> → <c>"50 4B 03 04"</c></example>
    /// </summary>
    public static string FormatHex(string hex)
    {
        if (string.IsNullOrEmpty(hex)) return hex;
        var sb = new StringBuilder(hex.Length + hex.Length / 2);
        for (var i = 0; i < hex.Length; i += 2)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(hex, i, Math.Min(2, hex.Length - i));
        }
        return sb.ToString();
    }

    // ------------------------------------------------------------------
    // Private rendering helpers
    // ------------------------------------------------------------------

    private static void AppendHeader(StringBuilder sb, EmbeddedFormatEntry entry)
    {
        sb.AppendLine($"Name        : {entry.Name}");
        sb.AppendLine($"Category    : {entry.Category}");
        sb.AppendLine($"Description : {entry.Description}");
        sb.AppendLine($"Extensions  : {string.Join("  ", entry.Extensions)}");

        if (entry.MimeTypes is { Count: > 0 })
            sb.AppendLine($"MIME        : {string.Join("  ", entry.MimeTypes)}");

        sb.AppendLine($"Quality     : {entry.QualityScore}/100");

        if (!string.IsNullOrEmpty(entry.PreferredEditor))
            sb.AppendLine($"Editor      : {entry.PreferredEditor}");

        if (!string.IsNullOrEmpty(entry.DiffMode))
            sb.AppendLine($"Diff mode   : {entry.DiffMode}");

        if (!string.IsNullOrEmpty(entry.Platform))
            sb.AppendLine($"Platform    : {entry.Platform}");

        if (entry.Signatures is { Count: > 0 })
        {
            sb.AppendLine("Signatures  :");
            foreach (var sig in entry.Signatures)
                sb.AppendLine($"  @{sig.Offset,4}  {FormatHex(sig.Value)}  weight={sig.Weight:F2}");
        }
    }

    private static void AppendMarkdownHeader(StringBuilder sb, EmbeddedFormatEntry entry)
    {
        sb.AppendLine($"## {entry.Name}");
        sb.AppendLine();
        sb.AppendLine($"> {entry.Description}");
        sb.AppendLine();
        sb.AppendLine("| Field | Value |");
        sb.AppendLine("|---|---|");
        sb.AppendLine($"| Category | `{entry.Category}` |");
        sb.AppendLine($"| Extensions | {string.Join(" ", entry.Extensions.Select(e => $"`{e}`"))} |");

        if (entry.MimeTypes is { Count: > 0 })
            sb.AppendLine($"| MIME | {string.Join(", ", entry.MimeTypes.Select(m => $"`{m}`"))} |");

        sb.AppendLine($"| Quality | {entry.QualityScore}/100 |");
        sb.AppendLine($"| Text format | {(entry.IsTextFormat ? "yes" : "no")} |");

        if (!string.IsNullOrEmpty(entry.PreferredEditor))
            sb.AppendLine($"| Preferred editor | `{entry.PreferredEditor}` |");

        if (!string.IsNullOrEmpty(entry.DiffMode))
            sb.AppendLine($"| Diff mode | `{entry.DiffMode}` |");

        if (!string.IsNullOrEmpty(entry.Platform))
            sb.AppendLine($"| Platform | {entry.Platform} |");

        if (entry.Signatures is { Count: > 0 })
        {
            sb.AppendLine();
            sb.AppendLine("### Magic Bytes");
            sb.AppendLine("| Offset | Signature | Weight |");
            sb.AppendLine("|---|---|---|");
            foreach (var sig in entry.Signatures)
                sb.AppendLine($"| `0x{sig.Offset:X4}` | `{FormatHex(sig.Value)}` | {sig.Weight:F2} |");
        }
    }

    private static void AppendRichPlainText(StringBuilder sb, FormatMetadata meta)
    {
        if (meta.Forensic is { } f)
        {
            sb.AppendLine($"Forensic    : {f.Category} / risk={f.RiskLevel}");
            foreach (var p in f.SuspiciousPatterns)
                sb.AppendLine($"  ⚠ {p.Name}");
        }

        if (meta.AiHints?.AnalysisContext is { } ctx)
            sb.AppendLine($"AI context  : {ctx[..Math.Min(120, ctx.Length)]}…");

        if (meta.TechnicalDetails is { } td)
        {
            var parts = new List<string>(3);
            if (td.Endianness is not null)        parts.Add($"endian={td.Endianness}");
            if (td.CompressionMethod is not null) parts.Add($"compress={td.CompressionMethod}");
            if (td.SupportsEncryption == true)    parts.Add("encrypted");
            if (parts.Count > 0) sb.AppendLine($"Technical   : {string.Join("  ", parts)}");
        }
    }

    private static void AppendRichMarkdown(StringBuilder sb, FormatMetadata meta)
    {
        if (meta.Forensic is { } f)
        {
            sb.AppendLine();
            sb.AppendLine($"### Forensic ({f.RiskLevel} risk)");
            foreach (var p in f.SuspiciousPatterns)
                sb.AppendLine($"- ⚠ **{p.Name}** — {p.Description}");
        }

        if (meta.Bookmarks.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("### Navigation Bookmarks");
            foreach (var b in meta.Bookmarks)
                sb.AppendLine($"- **{b.Name}** at `{b.Offset?.ToString("X4") ?? b.OffsetVar ?? "?"}`");
        }

        if (meta.Assertions.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("### Validation Assertions");
            foreach (var a in meta.Assertions)
                sb.AppendLine($"- `[{a.Severity}]` **{a.Name}**: `{a.Expression}`");
        }
    }
}
