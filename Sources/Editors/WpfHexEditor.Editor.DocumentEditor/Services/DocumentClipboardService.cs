// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor
// File: Services/DocumentClipboardService.cs
// Description:
//     Rich Copy/Paste: writes plain-text + HTML + RTF payloads
//     onto the system clipboard so external apps (Word, browser,
//     mail) receive formatted text, and reads RTF/HTML back when
//     pasting from those apps.
// Architecture notes:
//     Plain text is always written (lowest-common denominator).
//     HTML uses the CF_HTML descriptor preamble required by
//     Windows clipboard. RTF reuses RtfSchemaEngine driven by
//     the embedded RTF.whfmt — same path as RtfDocumentSaver,
//     so output is consistent with file-level Save As.
// ==========================================================

using System.Globalization;
using System.Net;
using System.Text;
using System.Windows;
using WpfHexEditor.Core.Definitions;
using WpfHexEditor.Editor.DocumentEditor.Core.Model;
using WpfHexEditor.Editor.DocumentEditor.Core.Schema;

namespace WpfHexEditor.Editor.DocumentEditor.Services;

/// <summary>
/// Builds and consumes rich clipboard payloads (plain / HTML / RTF) for
/// DocumentEditor copy and paste operations.
/// </summary>
public static class DocumentClipboardService
{
    // RTF.whfmt is parsed once on first Copy and cached for the process lifetime
    // (EmbeddedFormatCatalog is immutable at runtime).
    private static readonly Lazy<DocumentSchemaDefinition?> _rtfSchema =
        new(LoadRtfSchemaCore, isThreadSafe: true);

    /// <summary>
    /// Copies <paramref name="blocks"/> + <paramref name="plainText"/> onto the
    /// system clipboard with three formats: plain text, HTML, and RTF.
    /// </summary>
    public static void CopyRich(IReadOnlyList<DocumentBlock> blocks, string plainText)
    {
        var data = new DataObject();
        data.SetText(plainText ?? string.Empty);

        if (blocks is { Count: > 0 })
        {
            string html = BuildHtmlFragment(blocks);
            if (!string.IsNullOrEmpty(html))
                data.SetData(DataFormats.Html, WrapCfHtml(html));

            string rtf = BuildRtf(blocks);
            if (!string.IsNullOrEmpty(rtf))
                data.SetData(DataFormats.Rtf, rtf);
        }

        try { Clipboard.SetDataObject(data, copy: true); }
        catch (System.Runtime.InteropServices.COMException) { /* clipboard busy — best-effort */ }
    }

    /// <summary>
    /// Returns the richest available clipboard payload as plain text. A single
    /// <see cref="Clipboard.GetDataObject"/> call drives all subsequent reads to
    /// avoid multiple COM round-trips.
    /// </summary>
    public static string GetTextFromClipboard()
    {
        try
        {
            var data = Clipboard.GetDataObject();
            if (data is null) return string.Empty;

            // Prefer HTML body (stripped) over plain text — preserves layout cues.
            if (data.GetDataPresent(DataFormats.Html) && data.GetData(DataFormats.Html) is string cfHtml)
            {
                var body = ExtractHtmlBody(cfHtml);
                if (!string.IsNullOrEmpty(body)) return StripHtmlTags(body);
            }
            if (data.GetDataPresent(DataFormats.UnicodeText))
                return data.GetData(DataFormats.UnicodeText) as string ?? string.Empty;
            if (data.GetDataPresent(DataFormats.Text))
                return data.GetData(DataFormats.Text) as string ?? string.Empty;
        }
        catch (System.Runtime.InteropServices.COMException) { /* clipboard busy */ }
        return string.Empty;
    }

    // ── HTML fragment ──────────────────────────────────────────────────────

    private static string BuildHtmlFragment(IReadOnlyList<DocumentBlock> blocks)
    {
        var sb = new StringBuilder();
        foreach (var b in blocks) AppendHtmlBlock(sb, b);
        return sb.ToString();
    }

    private static void AppendHtmlBlock(StringBuilder sb, DocumentBlock b)
    {
        if (b.Kind == DocumentBlockKinds.Run)
        {
            AppendInlineRun(sb, b);
            return;
        }
        if (b.Kind == DocumentBlockKinds.Image)
        {
            sb.Append("<img alt=\"image\"/>");
            return;
        }

        string tag = b.Kind switch
        {
            DocumentBlockKinds.Heading    => HeadingTag(b),
            DocumentBlockKinds.ListItem   => "li",
            DocumentBlockKinds.Table      => "table",
            DocumentBlockKinds.TableRow   => "tr",
            DocumentBlockKinds.TableCell  => "td",
            DocumentBlockKinds.Hyperlink  => "a",
            _                             => "p"
        };

        sb.Append('<').Append(tag);
        AppendStyleAttr(sb, b);
        sb.Append('>');

        if (b.Children.Count == 0)
            AppendHtmlEncoded(sb, b.Text);
        else
            foreach (var c in b.Children) AppendHtmlBlock(sb, c);

        sb.Append("</").Append(tag).Append('>');
    }

    private static void AppendInlineRun(StringBuilder sb, DocumentBlock run)
    {
        sb.Append("<span");
        AppendStyleAttr(sb, run);
        sb.Append('>');
        AppendHtmlEncoded(sb, run.Text);
        sb.Append("</span>");
    }

    private static string HeadingTag(DocumentBlock b)
    {
        int level = b.Attributes.TryGetValue(DocumentBlockAttributes.Level, out var l) && l is int li
            ? Math.Clamp(li, 1, 6) : 1;
        return "h" + level;
    }

    private static void AppendStyleAttr(StringBuilder sb, DocumentBlock b)
    {
        int before = sb.Length;
        sb.Append(" style=\"");
        int styleStart = sb.Length;
        AppendCssStyle(sb, b);
        if (sb.Length == styleStart)
        {
            sb.Length = before;
            return;
        }
        sb.Append('"');
    }

    private static void AppendCssStyle(StringBuilder sb, DocumentBlock b)
    {
        if (b.Attributes.TryGetValue(DocumentBlockAttributes.FontFamily, out var ff) && ff is string s && IsSafeFontFamily(s))
            sb.Append("font-family:").Append(s).Append(';');
        if (b.Attributes.TryGetValue(DocumentBlockAttributes.FontSize, out var fs))
        {
            double pt = fs switch { double d => d, int i => i, _ => 0 };
            if (pt > 0) sb.Append("font-size:").Append(pt.ToString(CultureInfo.InvariantCulture)).Append("pt;");
        }
        if (b.Attributes.TryGetValue(DocumentBlockAttributes.Bold,      out var bo) && bo is true) sb.Append("font-weight:bold;");
        if (b.Attributes.TryGetValue(DocumentBlockAttributes.Italic,    out var it) && it is true) sb.Append("font-style:italic;");
        if (b.Attributes.TryGetValue(DocumentBlockAttributes.Underline, out var un) && un is true) sb.Append("text-decoration:underline;");
        if (b.Attributes.TryGetValue(DocumentBlockAttributes.Color,     out var c)  && c  is string cs && IsSafeColor(cs))
            sb.Append("color:").Append(cs).Append(';');
    }

    /// <summary>
    /// Rejects font-family strings that contain CSS-breakout characters
    /// (<c>;</c>, <c>{</c>, <c>}</c>, <c>&lt;</c>, <c>&gt;</c>, <c>"</c>) so a
    /// crafted attribute can't inject extra declarations into clipboard HTML.
    /// </summary>
    private static bool IsSafeFontFamily(string s)
    {
        foreach (char ch in s)
            if (ch is ';' or '{' or '}' or '<' or '>' or '"') return false;
        return s.Length > 0 && s.Length < 200;
    }

    /// <summary>Accepts <c>#RGB</c>/<c>#RRGGBB</c>/<c>#RRGGBBAA</c> only.</summary>
    private static bool IsSafeColor(string s)
    {
        if (s.Length is not (4 or 7 or 9) || s[0] != '#') return false;
        for (int i = 1; i < s.Length; i++)
        {
            char ch = s[i];
            if (!((ch >= '0' && ch <= '9') || (ch >= 'a' && ch <= 'f') || (ch >= 'A' && ch <= 'F')))
                return false;
        }
        return true;
    }

    private static void AppendHtmlEncoded(StringBuilder sb, string text) =>
        sb.Append(WebUtility.HtmlEncode(text));

    /// <summary>
    /// Wraps an HTML fragment in the CF_HTML descriptor envelope.
    /// Offsets are in UTF-8 bytes (per spec) — char-count would corrupt
    /// fragments containing non-ASCII (accents, emoji, CJK).
    /// </summary>
    private static string WrapCfHtml(string fragmentHtml)
    {
        // CF_HTML header is fixed-width (5 fields × :D10 + labels + CRLFs) = 105 ASCII bytes.
        const string headerFmt =
            "Version:0.9\r\n" +
            "StartHTML:{0:D10}\r\n" +
            "EndHTML:{1:D10}\r\n" +
            "StartFragment:{2:D10}\r\n" +
            "EndFragment:{3:D10}\r\n";
        const int headerLen = 105;

        const string startMarker = "<!--StartFragment-->";
        const string endMarker   = "<!--EndFragment-->";

        string html = "<html><body>" + startMarker + fragmentHtml + endMarker + "</body></html>";

        int markerStartChars = html.IndexOf(startMarker, StringComparison.Ordinal);
        int markerEndChars   = html.IndexOf(endMarker,   StringComparison.Ordinal);

        int startHtml     = headerLen;
        int startFragment = startHtml + Encoding.UTF8.GetByteCount(html.AsSpan(0, markerStartChars + startMarker.Length));
        int endFragment   = startHtml + Encoding.UTF8.GetByteCount(html.AsSpan(0, markerEndChars));
        int endHtml       = startHtml + Encoding.UTF8.GetByteCount(html);

        return string.Format(headerFmt, startHtml, endHtml, startFragment, endFragment) + html;
    }

    private static string ExtractHtmlBody(string? cfHtml)
    {
        if (string.IsNullOrEmpty(cfHtml)) return string.Empty;
        int s = cfHtml.IndexOf("<!--StartFragment-->", StringComparison.Ordinal);
        int e = cfHtml.IndexOf("<!--EndFragment-->",   StringComparison.Ordinal);
        if (s < 0 || e < 0 || e <= s) return cfHtml;
        s += "<!--StartFragment-->".Length;
        return cfHtml[s..e];
    }

    private static string StripHtmlTags(string html)
    {
        var sb = new StringBuilder(html.Length);
        bool inTag = false;
        foreach (char ch in html)
        {
            if (ch == '<') { inTag = true; continue; }
            if (ch == '>') { inTag = false; sb.Append(' '); continue; }
            if (!inTag) sb.Append(ch);
        }
        return WebUtility.HtmlDecode(sb.ToString());
    }

    // ── RTF payload (reuses RtfSchemaEngine + embedded RTF.whfmt) ──────────

    private static string BuildRtf(IReadOnlyList<DocumentBlock> blocks)
    {
        var schema = _rtfSchema.Value;
        return schema is not null
            ? RtfSchemaEngine.SerializeBlocks(blocks, schema)
            : FallbackRtf(blocks);
    }

    private static DocumentSchemaDefinition? LoadRtfSchemaCore()
    {
        try
        {
            var catalog = EmbeddedFormatCatalog.Instance;
            var key = catalog.GetAll()
                .Select(e => e.ResourceKey)
                .FirstOrDefault(k => k is not null &&
                    k.EndsWith("RTF.whfmt", StringComparison.OrdinalIgnoreCase));
            if (key is null) return null;
            return DocumentSchemaReader.ReadFromJson(catalog.GetJson(key), "RTF.whfmt");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DocumentClipboardService] RTF schema load failed: {ex.Message}");
            return null;
        }
    }

    private const string FallbackRtfHeader =
        @"{\rtf1\ansi\ansicpg1252\deff0{\fonttbl{\f0\fnil\fcharset0 Times New Roman;}}\f0\fs24" + " ";

    private static string FallbackRtf(IReadOnlyList<DocumentBlock> blocks)
    {
        var sb = new StringBuilder(FallbackRtfHeader.Length + blocks.Count * 32);
        sb.Append(FallbackRtfHeader);
        foreach (var b in blocks)
            sb.Append(@"\pard\plain ").Append(RtfSchemaEngine.EscapeText(b.Text)).Append(@"\par");
        sb.Append('}');
        return sb.ToString();
    }
}
