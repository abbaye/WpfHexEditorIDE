// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor.Core
// File: Helpers/DocumentLoaderHelpers.cs
// Description:
//     Shared helpers used by all DocumentEditor format loaders
//     (DOCX, ODT, RTF, FlatODT, EPUB). Centralizes stream
//     buffering, date parsing and HTML tag stripping so each
//     loader doesn't reimplement them.
// ==========================================================

namespace WpfHexEditor.Editor.DocumentEditor.Core.Helpers;

/// <summary>Helpers shared by IDocumentLoader implementations.</summary>
public static class DocumentLoaderHelpers
{
    /// <summary>
    /// Buffers a stream into a byte[]. Fast-paths a MemoryStream whose
    /// internal buffer is exposed; otherwise copies through a temporary
    /// MemoryStream. Used by every loader that needs both ZipArchive
    /// access and a raw byte[] for forensic analysis.
    /// </summary>
    public static async Task<byte[]> BufferStreamAsync(Stream stream, CancellationToken ct = default)
    {
        if (stream is MemoryStream ms && ms.TryGetBuffer(out _)) return ms.ToArray();
        using var buf = new MemoryStream();
        await stream.CopyToAsync(buf, ct);
        return buf.ToArray();
    }

    /// <summary>
    /// Parses a date string in any culture-invariant ISO-ish format,
    /// returning UTC DateTime. Returns null for null/empty/malformed input.
    /// </summary>
    public static DateTime? TryParseDate(string? value) =>
        DateTime.TryParse(value, out var dt) ? dt.ToUniversalTime() : null;

    /// <summary>
    /// Strips HTML/XML tags and collapses internal whitespace. Used by
    /// the EPUB tag-soup fallback and the clipboard HTML→plain path.
    /// </summary>
    public static string StripHtmlTags(string? html)
    {
        if (string.IsNullOrEmpty(html)) return string.Empty;
        var sb = new System.Text.StringBuilder(html.Length);
        bool inTag = false;
        foreach (char ch in html)
        {
            if (ch == '<') { inTag = true; continue; }
            if (ch == '>') { inTag = false; sb.Append(' '); continue; }
            if (!inTag) sb.Append(ch);
        }
        return System.Net.WebUtility.HtmlDecode(sb.ToString());
    }
}
