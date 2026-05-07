// ==========================================================
// Project: whfmt.Analysis
// File: DiffRenderer.cs
// Description: Renders DiffResult to text, JSON, or HTML.
// ==========================================================

using System.Text;
using System.Text.Json;

namespace WhfmtAnalysis;

/// <summary>Renders a <see cref="DiffResult"/> to text, JSON, or HTML.</summary>
public static class DiffRenderer
{
    /// <summary>Render as human-readable plain text.</summary>
    public static string RenderText(DiffResult r)
    {
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine($"  whfmt diff — {r.FormatName}");
        sb.AppendLine($"  {"─".PadRight(60, '─')}");
        sb.AppendLine($"  A: {r.FileA}  ({FormatSize(r.SizeA)})");
        sb.AppendLine($"  B: {r.FileB}  ({FormatSize(r.SizeB)})");

        if (r.Error is not null) { sb.AppendLine($"  ERROR: {r.Error}"); return sb.ToString(); }

        sb.AppendLine($"  Format: {r.FormatName}  |  {(r.FormatsMatch ? "same format" : $"A={r.FormatDetectedA} B={r.FormatDetectedB}")}");
        sb.AppendLine($"  Size delta: {(r.RawByteDelta >= 0 ? "+" : "")}{r.RawByteDelta} bytes");
        sb.AppendLine();

        if (r.IsIdentical)
        {
            sb.AppendLine("  ✓ Files are semantically IDENTICAL (all key fields match).");
            sb.AppendLine();
            return sb.ToString();
        }

        var changed   = r.FieldChanges.Where(f => !f.IsIgnored && f.IsChanged).ToList();
        var unchanged = r.FieldChanges.Where(f => !f.IsIgnored && !f.IsChanged).ToList();
        var ignored   = r.FieldChanges.Where(f => f.IsIgnored).ToList();

        if (changed.Count > 0)
        {
            sb.AppendLine($"  Changed fields ({changed.Count}):");
            foreach (var c in changed)
                sb.AppendLine($"    ≠  {c.FieldName,-30}  A: {c.ValueA}  →  B: {c.ValueB}");
            sb.AppendLine();
        }

        if (unchanged.Count > 0)
        {
            sb.AppendLine($"  Unchanged fields ({unchanged.Count}):");
            foreach (var c in unchanged)
                sb.AppendLine($"    =  {c.FieldName,-30}  {c.ValueA}");
            sb.AppendLine();
        }

        if (ignored.Count > 0)
        {
            sb.AppendLine($"  Ignored fields ({ignored.Count} — noise excluded by format definition):");
            foreach (var c in ignored)
                sb.AppendLine($"    ·  {c.FieldName,-30}  A: {c.ValueA}  /  B: {c.ValueB}");
            sb.AppendLine();
        }

        sb.AppendLine($"  Result: {changed.Count} change(s), {unchanged.Count} match(es), {ignored.Count} ignored");
        sb.AppendLine();
        return sb.ToString();
    }

    /// <summary>Render as JSON.</summary>
    public static string RenderJson(DiffResult r)
    {
        var model = new
        {
            fileA         = r.FileA,
            fileB         = r.FileB,
            sizeA         = r.SizeA,
            sizeB         = r.SizeB,
            format        = r.FormatName,
            formatsMatch  = r.FormatsMatch,
            isIdentical   = r.IsIdentical,
            rawByteDelta  = r.RawByteDelta,
            changedCount  = r.ChangedCount,
            unchangedCount= r.UnchangedCount,
            error         = r.Error,
            fields        = r.FieldChanges.Select(f => new { f.FieldName, f.ValueA, f.ValueB, f.IsChanged, f.IsIgnored })
        };
        return JsonSerializer.Serialize(model, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>Render as self-contained dark-themed HTML.</summary>
    public static string RenderHtml(DiffResult r)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html lang=\"en\"><head>");
        sb.AppendLine("<meta charset=\"UTF-8\"><title>whfmt diff</title>");
        sb.AppendLine(HtmlStyle());
        sb.AppendLine("</head><body><div class=\"container\">");
        sb.AppendLine($"<h1>whfmt <span class=\"accent\">diff</span> — {Esc(r.FormatName)}</h1>");
        sb.AppendLine($"<div class=\"meta\"><span class=\"label\">A</span> {Esc(r.FileA)} <span class=\"size\">{FormatSize(r.SizeA)}</span></div>");
        sb.AppendLine($"<div class=\"meta\"><span class=\"label\">B</span> {Esc(r.FileB)} <span class=\"size\">{FormatSize(r.SizeB)}</span></div>");
        sb.AppendLine($"<div class=\"meta\"><span class=\"label\">Size Δ</span> {(r.RawByteDelta >= 0 ? "+" : "")}{r.RawByteDelta} bytes</div>");

        if (r.Error is not null) { sb.AppendLine($"<div class=\"error\">{Esc(r.Error)}</div>"); }
        else if (r.IsIdentical)  { sb.AppendLine("<div class=\"status identical\">✓ Semantically IDENTICAL</div>"); }
        else                     { sb.AppendLine($"<div class=\"status changed\">{r.ChangedCount} field(s) changed</div>"); }

        if (r.FieldChanges.Count > 0)
        {
            sb.AppendLine("<table><thead><tr><th>Field</th><th>Value A</th><th>Value B</th><th>Status</th></tr></thead><tbody>");
            foreach (var f in r.FieldChanges)
            {
                string cls = f.IsIgnored ? "ignored" : f.IsChanged ? "changed" : "same";
                string status = f.IsIgnored ? "ignored" : f.IsChanged ? "≠ changed" : "= same";
                sb.AppendLine($"<tr class=\"{cls}\"><td>{Esc(f.FieldName)}</td><td>{Esc(f.ValueA)}</td><td>{Esc(f.ValueB)}</td><td>{status}</td></tr>");
            }
            sb.AppendLine("</tbody></table>");
        }

        sb.AppendLine($"<footer>Generated by <a href=\"https://github.com/abbaye/WpfHexEditorIDE\">whfmt.Analysis</a> {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC</footer>");
        sb.AppendLine("</div></body></html>");
        return sb.ToString();
    }

    private static string HtmlStyle() => """
        <style>
        *{box-sizing:border-box;margin:0;padding:0}
        body{font-family:'Segoe UI',system-ui,sans-serif;background:#0d1117;color:#c9d1d9;padding:2rem}
        .container{max-width:900px;margin:0 auto}
        h1{font-size:1.8rem;margin-bottom:1rem;color:#58a6ff}.accent{color:#f78166}
        .meta{margin:.3rem 0;font-size:.9rem}.label{color:#8b949e;min-width:60px;display:inline-block}
        .size{color:#8b949e;font-size:.85rem;margin-left:.5rem}
        .status{margin:1rem 0;padding:.6rem 1rem;border-radius:6px;font-weight:600}
        .identical{background:#0f2a1a;border:1px solid #2ea043;color:#3fb950}
        .changed{background:#2a0f0f;border:1px solid #f85149;color:#f85149}
        .error{background:#2a1a0a;border:1px solid #d29922;color:#d29922;padding:.6rem 1rem;border-radius:6px;margin:1rem 0}
        table{width:100%;border-collapse:collapse;margin-top:1rem;font-size:.88rem}
        th{background:#161b22;padding:.5rem .8rem;text-align:left;border:1px solid #30363d;color:#8b949e}
        td{padding:.4rem .8rem;border:1px solid #21262d}
        tr.changed td{background:#1a0a0a}tr.changed td:nth-child(2){color:#f85149}tr.changed td:nth-child(3){color:#3fb950}
        tr.ignored td{color:#484f58;font-style:italic}
        footer{margin-top:2rem;color:#484f58;font-size:.8rem}footer a{color:#58a6ff;text-decoration:none}
        </style>
        """;

    private static string Esc(string s) => s.Replace("&","&amp;").Replace("<","&lt;").Replace(">","&gt;");
    private static string FormatSize(long b) => b < 1024 ? $"{b} B" : b < 1048576 ? $"{b/1024.0:F1} KB" : $"{b/1048576.0:F2} MB";
}
