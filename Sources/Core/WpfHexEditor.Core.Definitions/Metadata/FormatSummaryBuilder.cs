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
    /// </summary>
    public static string BuildPlainText(EmbeddedFormatEntry entry, IEmbeddedFormatCatalog? catalog = null)
    {
        var sb = new StringBuilder();
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
            sb.AppendLine($"Signatures  :");
            foreach (var sig in entry.Signatures)
                sb.AppendLine($"  @{sig.Offset,4}  {FormatHex(sig.Value)}  weight={sig.Weight:F2}");
        }

        if (catalog is not null)
            AppendRichPlainText(sb, entry, catalog);

        return sb.ToString();
    }

    // ------------------------------------------------------------------
    // Markdown
    // ------------------------------------------------------------------

    /// <summary>
    /// Returns a Markdown-formatted summary card for the format.
    /// Suitable for README files, tooltips, or preview panes.
    /// </summary>
    public static string BuildMarkdown(EmbeddedFormatEntry entry, IEmbeddedFormatCatalog? catalog = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"## {entry.Name}");
        sb.AppendLine();
        sb.AppendLine($"> {entry.Description}");
        sb.AppendLine();
        sb.AppendLine($"| Field | Value |");
        sb.AppendLine($"|---|---|");
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

        if (catalog is not null)
            AppendRichMarkdown(sb, entry, catalog);

        return sb.ToString();
    }

    // ------------------------------------------------------------------
    // Diagnostic dump
    // ------------------------------------------------------------------

    /// <summary>
    /// Returns a verbose diagnostic dump for debugging.
    /// Includes the resource key, all metadata fields, and rich blocks if <paramref name="catalog"/> is provided.
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
            var forensic = entry.GetForensicSummary(catalog);
            if (forensic is not null)
            {
                sb.AppendLine($"Forensic.Category   : {forensic.Category}");
                sb.AppendLine($"Forensic.RiskLevel  : {forensic.RiskLevel}  (high={forensic.IsHighRisk})");
                if (forensic.SuspiciousPatterns.Count > 0)
                    foreach (var p in forensic.SuspiciousPatterns)
                        sb.AppendLine($"  SUSPICIOUS: {p.Name} — {p.Description}");
            }

            var assertions = entry.GetAssertions(catalog);
            if (assertions.Count > 0)
            {
                sb.AppendLine($"Assertions ({assertions.Count}):");
                foreach (var a in assertions)
                    sb.AppendLine($"  [{a.Severity.ToUpperInvariant()}] {a.Name}: {a.Expression}");
            }

            var bookmarks = entry.GetNavigationBookmarks(catalog);
            if (bookmarks.Count > 0)
            {
                sb.AppendLine($"Bookmarks ({bookmarks.Count}):");
                foreach (var b in bookmarks)
                    sb.AppendLine($"  {b.Name}  offset={b.Offset?.ToString() ?? b.OffsetVar ?? "?"}  icon={b.Icon}");
            }

            var exports = entry.GetExportTemplates(catalog);
            if (exports.Count > 0)
                foreach (var e in exports)
                    sb.AppendLine($"ExportTemplate: {e.Name} [{e.Format}]  fields=[{string.Join(", ", e.Fields)}]");

            var td = entry.GetTechnicalDetails(catalog);
            if (td is not null)
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
    // Signature helpers
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
    // Private rich-block helpers
    // ------------------------------------------------------------------

    private static void AppendRichPlainText(StringBuilder sb, EmbeddedFormatEntry entry, IEmbeddedFormatCatalog catalog)
    {
        var forensic = entry.GetForensicSummary(catalog);
        if (forensic is not null)
        {
            sb.AppendLine($"Forensic    : {forensic.Category} / risk={forensic.RiskLevel}");
            foreach (var p in forensic.SuspiciousPatterns)
                sb.AppendLine($"  ⚠ {p.Name}");
        }

        var ai = entry.GetAiHints(catalog);
        if (ai?.AnalysisContext is not null)
            sb.AppendLine($"AI context  : {ai.AnalysisContext[..Math.Min(120, ai.AnalysisContext.Length)]}…");

        var td = entry.GetTechnicalDetails(catalog);
        if (td is not null)
        {
            var parts = new List<string>();
            if (td.Endianness is not null)        parts.Add($"endian={td.Endianness}");
            if (td.CompressionMethod is not null) parts.Add($"compress={td.CompressionMethod}");
            if (td.SupportsEncryption == true)    parts.Add("encrypted");
            if (parts.Count > 0) sb.AppendLine($"Technical   : {string.Join("  ", parts)}");
        }
    }

    private static void AppendRichMarkdown(StringBuilder sb, EmbeddedFormatEntry entry, IEmbeddedFormatCatalog catalog)
    {
        var forensic = entry.GetForensicSummary(catalog);
        if (forensic is not null)
        {
            sb.AppendLine();
            sb.AppendLine($"### Forensic ({forensic.RiskLevel} risk)");
            if (forensic.SuspiciousPatterns.Count > 0)
                foreach (var p in forensic.SuspiciousPatterns)
                    sb.AppendLine($"- ⚠ **{p.Name}** — {p.Description}");
        }

        var bookmarks = entry.GetNavigationBookmarks(catalog);
        if (bookmarks.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("### Navigation Bookmarks");
            foreach (var b in bookmarks)
                sb.AppendLine($"- **{b.Name}** at `{b.Offset?.ToString("X4") ?? b.OffsetVar ?? "?"}`");
        }

        var assertions = entry.GetAssertions(catalog);
        if (assertions.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("### Validation Assertions");
            foreach (var a in assertions)
                sb.AppendLine($"- `[{a.Severity}]` **{a.Name}**: `{a.Expression}`");
        }
    }
}
