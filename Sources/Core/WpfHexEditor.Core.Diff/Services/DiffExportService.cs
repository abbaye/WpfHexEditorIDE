// Project      : WpfHexEditorControl
// File         : Services/DiffExportService.cs
// Description  : Exports diff results in Unified Patch, HTML, or plain-text formats.
// Architecture : Stateless service — pure string transformation, no WPF.

using System.Text;
using WpfHexEditor.Core.Diff.Models;

namespace WpfHexEditor.Core.Diff.Services;

/// <summary>
/// Generates human-readable or machine-readable diff reports from <see cref="DiffEngineResult"/>.
/// </summary>
public sealed class DiffExportService
{
    private const int UnifiedContext = 3; // lines of context in unified patch

    // -----------------------------------------------------------------------
    // Unified diff patch format (.patch)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Generates a unified diff patch string from a text comparison result.
    /// </summary>
    public string ExportUnifiedPatch(TextDiffResult result, string leftPath, string rightPath)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"--- a/{EscapePath(leftPath)}");
        sb.AppendLine($"+++ b/{EscapePath(rightPath)}");

        // Group lines into hunks
        var lines  = result.Lines;
        var hunks  = BuildHunks(lines, UnifiedContext);

        foreach (var hunk in hunks)
        {
            var leftStart  = hunk.LeftStart;
            var rightStart = hunk.RightStart;
            var leftCount  = hunk.Lines.Count(l => l.Kind is TextLineKind.Equal or TextLineKind.DeletedLeft  or TextLineKind.Modified);
            var rightCount = hunk.Lines.Count(l => l.Kind is TextLineKind.Equal or TextLineKind.InsertedRight or TextLineKind.Modified);

            sb.AppendLine($"@@ -{leftStart},{leftCount} +{rightStart},{rightCount} @@");

            foreach (var line in hunk.Lines)
            {
                sb.AppendLine(line.Kind switch
                {
                    TextLineKind.Equal         => $" {line.Content}",
                    TextLineKind.DeletedLeft   => $"-{line.Content}",
                    TextLineKind.InsertedRight => $"+{line.Content}",
                    TextLineKind.Modified      => line.LeftLineNumber.HasValue ? $"-{line.Content}" : $"+{line.Content}",
                    _                          => $" {line.Content}"
                });
            }
        }

        return sb.ToString();
    }

    // -----------------------------------------------------------------------
    // HTML report (self-contained with inline CSS)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Generates a self-contained HTML report from any <see cref="DiffEngineResult"/>.
    /// </summary>
    public string ExportHtmlReport(DiffEngineResult result)
    {
        var sb  = new StringBuilder();
        var now = DateTime.Now;

        sb.Append("""
            <!DOCTYPE html>
            <html lang="en">
            <head>
            <meta charset="utf-8"/>
            <title>Diff Report</title>
            <style>
            body{font-family:Consolas,'Cascadia Code',monospace;font-size:13px;background:#1e1e1e;color:#d4d4d4;margin:0;padding:16px}
            h1{font-size:16px;font-weight:600;color:#d7ba7d;margin:0 0 8px}
            .meta{font-size:11px;color:#858585;margin-bottom:16px}
            .stats{display:flex;gap:16px;margin-bottom:16px;font-size:12px}
            .stat{background:#2d2d30;padding:6px 12px;border-radius:4px}
            .stat b{color:#9cdcfe}
            table{border-collapse:collapse;width:100%}
            td{padding:1px 6px;vertical-align:top;white-space:pre-wrap;word-break:break-all}
            .ln{width:50px;color:#858585;text-align:right;user-select:none;border-right:1px solid #3e3e42}
            .eq{background:transparent}
            .del{background:#4b1818}
            .ins{background:#183a1a}
            .mod{background:#2a2a0a}
            details>summary{cursor:pointer;color:#858585;padding:4px 6px;background:#252526;user-select:none}
            details[open]>summary{color:#d4d4d4}
            </style>
            </head>
            <body>
            """);

        sb.AppendLine($"<h1>Diff Report</h1>");
        sb.AppendLine($"<div class=\"meta\">Generated {now:yyyy-MM-dd HH:mm:ss} · Left: {HtmlEncode(result.LeftPath)} · Right: {HtmlEncode(result.RightPath)}</div>");

        if (result.TextResult is { } tr)
        {
            AppendHtmlTextStats(sb, tr.Stats);
            AppendHtmlTextTable(sb, tr.Lines);
        }
        else if (result.BinaryResult is { } br)
        {
            AppendHtmlBinaryStats(sb, br.Stats);
            AppendHtmlBinaryTable(sb, br.Regions);
        }

        sb.Append("</body></html>");
        return sb.ToString();
    }

    // -----------------------------------------------------------------------
    // Binary text report
    // -----------------------------------------------------------------------

    /// <summary>Generates an improved plain-text binary diff report.</summary>
    public string ExportBinaryDiffReport(BinaryDiffResult result, string leftPath, string rightPath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== Binary Diff Report ===");
        sb.AppendLine($"Left : {leftPath}");
        sb.AppendLine($"Right: {rightPath}");
        sb.AppendLine();
        sb.AppendLine("--- Statistics ---");
        sb.AppendLine($"Modified : {result.Stats.ModifiedCount} regions ({result.Stats.ModifiedBytes:N0} bytes)");
        sb.AppendLine($"Inserted : {result.Stats.InsertedCount} regions ({result.Stats.InsertedBytes:N0} bytes)");
        sb.AppendLine($"Deleted  : {result.Stats.DeletedCount} regions  ({result.Stats.DeletedBytes:N0} bytes)");
        sb.AppendLine($"Similarity: {result.Stats.Similarity:P1}");
        if (result.Truncated) sb.AppendLine($"WARNING: {result.TruncatedReason}");
        sb.AppendLine();
        sb.AppendLine("--- Regions (first 100) ---");
        sb.AppendLine();

        foreach (var region in result.Regions.Take(100))
        {
            sb.AppendLine($"Offset 0x{region.LeftOffset:X8} | {region.Kind} | {region.Length} bytes");
            if (region.LeftBytes.Length > 0)
                sb.AppendLine($"  Left : {FormatHex(region.LeftBytes, 32)}  {FormatAscii(region.LeftBytes, 32)}");
            if (region.RightBytes.Length > 0)
                sb.AppendLine($"  Right: {FormatHex(region.RightBytes, 32)}  {FormatAscii(region.RightBytes, 32)}");
            sb.AppendLine();
        }

        if (result.Regions.Count > 100)
            sb.AppendLine($"... and {result.Regions.Count - 100} more regions not shown.");

        return sb.ToString();
    }

    // -----------------------------------------------------------------------
    // Private HTML helpers
    // -----------------------------------------------------------------------

    private static void AppendHtmlTextStats(StringBuilder sb, TextDiffStats stats)
    {
        sb.AppendLine("<div class=\"stats\">");
        sb.AppendLine($"<div class=\"stat\"><b>{stats.EqualLines}</b> equal</div>");
        sb.AppendLine($"<div class=\"stat\"><b>{stats.ModifiedLines}</b> modified</div>");
        sb.AppendLine($"<div class=\"stat\"><b>{stats.DeletedLines}</b> deleted</div>");
        sb.AppendLine($"<div class=\"stat\"><b>{stats.InsertedLines}</b> inserted</div>");
        sb.AppendLine($"<div class=\"stat\">Similarity <b>{stats.Similarity:P1}</b></div>");
        sb.AppendLine("</div>");
    }

    private static void AppendHtmlTextTable(StringBuilder sb, IReadOnlyList<TextDiffLine> lines)
    {
        sb.AppendLine("<table>");
        // Group identical regions into collapsible blocks
        int i = 0;
        while (i < lines.Count)
        {
            var line = lines[i];
            if (line.Kind == TextLineKind.Equal)
            {
                int start = i;
                while (i < lines.Count && lines[i].Kind == TextLineKind.Equal) i++;
                int count = i - start;
                if (count > 6)
                {
                    sb.AppendLine($"<tr><td colspan=\"4\"><details><summary>... {count} identical lines ...</summary>");
                    for (int j = start; j < i; j++) AppendHtmlLine(sb, lines[j]);
                    sb.AppendLine("</details></td></tr>");
                }
                else
                {
                    for (int j = start; j < i; j++) AppendHtmlLine(sb, lines[j]);
                }
            }
            else
            {
                AppendHtmlLine(sb, line);
                i++;
            }
        }
        sb.AppendLine("</table>");
    }

    private static void AppendHtmlLine(StringBuilder sb, TextDiffLine line)
    {
        var cls = line.Kind switch
        {
            TextLineKind.Equal         => "eq",
            TextLineKind.DeletedLeft   => "del",
            TextLineKind.InsertedRight => "ins",
            TextLineKind.Modified      => "mod",
            _                          => "eq"
        };
        var prefix = line.Kind switch
        {
            TextLineKind.DeletedLeft   => "-",
            TextLineKind.InsertedRight => "+",
            TextLineKind.Modified      => line.LeftLineNumber.HasValue ? "-" : "+",
            _                          => " "
        };
        sb.AppendLine($"<tr class=\"{cls}\"><td class=\"ln\">{line.LeftLineNumber?.ToString() ?? ""}</td><td class=\"ln\">{line.RightLineNumber?.ToString() ?? ""}</td><td>{prefix}</td><td>{HtmlEncode(line.Content)}</td></tr>");
    }

    private static void AppendHtmlBinaryStats(StringBuilder sb, BinaryDiffStats stats)
    {
        sb.AppendLine("<div class=\"stats\">");
        sb.AppendLine($"<div class=\"stat\"><b>{stats.ModifiedCount}</b> modified</div>");
        sb.AppendLine($"<div class=\"stat\"><b>{stats.InsertedCount}</b> inserted</div>");
        sb.AppendLine($"<div class=\"stat\"><b>{stats.DeletedCount}</b> deleted</div>");
        sb.AppendLine($"<div class=\"stat\">Similarity <b>{stats.Similarity:P1}</b></div>");
        sb.AppendLine("</div>");
    }

    private static void AppendHtmlBinaryTable(StringBuilder sb, IReadOnlyList<BinaryDiffRegion> regions)
    {
        sb.AppendLine("<table>");
        sb.AppendLine("<tr><th>Offset</th><th>Kind</th><th>Left</th><th>Right</th></tr>");
        foreach (var r in regions.Take(500))
        {
            var cls = r.Kind switch
            {
                BinaryRegionKind.Modified        => "mod",
                BinaryRegionKind.InsertedInRight => "ins",
                BinaryRegionKind.DeletedInRight  => "del",
                _                                => "eq"
            };
            sb.AppendLine($"<tr class=\"{cls}\"><td>0x{r.LeftOffset:X8}</td><td>{r.Kind}</td><td>{HtmlEncode(FormatHex(r.LeftBytes, 16))}</td><td>{HtmlEncode(FormatHex(r.RightBytes, 16))}</td></tr>");
        }
        sb.AppendLine("</table>");
    }

    // -----------------------------------------------------------------------
    // Unified patch hunk builder
    // -----------------------------------------------------------------------

    private sealed class Hunk
    {
        public int  LeftStart  { get; set; }
        public int  RightStart { get; set; }
        public List<TextDiffLine> Lines { get; } = [];
    }

    private static List<Hunk> BuildHunks(IReadOnlyList<TextDiffLine> lines, int context)
    {
        var hunks = new List<Hunk>();
        Hunk? current = null;
        int lastChangedIdx = -1;

        for (int i = 0; i < lines.Count; i++)
        {
            bool isChange = lines[i].Kind != TextLineKind.Equal;
            if (isChange)
            {
                if (current is null)
                {
                    current = new Hunk
                    {
                        LeftStart  = Math.Max(1, (lines[i].LeftLineNumber  ?? i + 1) - context),
                        RightStart = Math.Max(1, (lines[i].RightLineNumber ?? i + 1) - context)
                    };
                    // Add preceding context
                    for (int c = Math.Max(0, i - context); c < i; c++)
                        current.Lines.Add(lines[c]);
                }
                lastChangedIdx = i;
                current.Lines.Add(lines[i]);
            }
            else if (current is not null)
            {
                current.Lines.Add(lines[i]);
                if (i - lastChangedIdx >= context)
                {
                    hunks.Add(current);
                    current = null;
                }
            }
        }

        if (current is not null) hunks.Add(current);
        return hunks;
    }

    // -----------------------------------------------------------------------
    // Formatting helpers
    // -----------------------------------------------------------------------

    private static string FormatHex(byte[] data, int maxBytes)
    {
        if (data.Length == 0) return string.Empty;
        var bytes = data.Length > maxBytes ? data[..maxBytes] : data;
        return BitConverter.ToString(bytes).Replace('-', ' ') + (data.Length > maxBytes ? "..." : "");
    }

    private static string FormatAscii(byte[] data, int maxBytes)
    {
        var bytes = data.Length > maxBytes ? data[..maxBytes] : data;
        return new string(bytes.Select(b => b >= 32 && b < 127 ? (char)b : '.').ToArray());
    }

    private static string EscapePath(string path) => path.Replace('\\', '/');

    private static string HtmlEncode(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
