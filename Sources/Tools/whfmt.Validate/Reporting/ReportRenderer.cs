// ==========================================================
// Project: whfmt.Validate
// File: Reporting/ReportRenderer.cs
// Description: Renders ValidationReport to text, JSON, or HTML.
// ==========================================================

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using WhfmtValidate.Engine;

namespace WhfmtValidate.Reporting;

internal static class ReportRenderer
{
    internal static string RenderText(ValidationReport r)
    {
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine($"  whfmt validate — {r.FileName}");
        sb.AppendLine($"  {"─".PadRight(60, '─')}");
        sb.AppendLine($"  File     : {r.FilePath}");
        sb.AppendLine($"  Size     : {FormatSize(r.FileSize)}");

        if (r.FileNotFound)
        {
            sb.AppendLine($"  ERROR    : File not found.");
            return sb.ToString();
        }

        sb.AppendLine($"  Format   : {r.FormatName} ({r.FormatCategory})");
        sb.AppendLine($"  Confidence: {r.Confidence:P0}  [{r.MatchSource}]");
        sb.AppendLine($"  Forensic : {r.ForensicRiskLevel.ToUpper()} risk");
        sb.AppendLine();

        if (r.Checks.Count > 0)
        {
            sb.AppendLine("  Passed checks:");
            foreach (var c in r.Checks)
                sb.AppendLine($"    ✓  [{c.Category}] {c.Name} — {c.Detail}");
            sb.AppendLine();
        }

        if (r.Issues.Count > 0)
        {
            sb.AppendLine("  Issues:");
            foreach (var i in r.Issues)
            {
                string icon = i.Severity switch { "error" => "✗", "warning" => "⚠", _ => "ℹ" };
                sb.AppendLine($"    {icon}  [{i.Severity.ToUpper()}] [{i.Category}] {i.Name}: {i.Message}");
            }
            sb.AppendLine();
        }

        string status = r.IsValid ? "✓ VALID" : $"✗ INVALID  ({r.ErrorCount} error(s), {r.WarningCount} warning(s))";
        sb.AppendLine($"  Result   : {status}");
        sb.AppendLine();
        return sb.ToString();
    }

    internal static string RenderJson(ValidationReport r)
    {
        var model = new
        {
            file        = r.FilePath,
            size        = r.FileSize,
            format      = r.FormatName,
            category    = r.FormatCategory,
            confidence  = r.Confidence,
            matchSource = r.MatchSource,
            forensicRisk = r.ForensicRiskLevel,
            isValid     = r.IsValid,
            errors      = r.ErrorCount,
            warnings    = r.WarningCount,
            checks      = r.Checks.Select(c => new { c.Category, c.Name, c.Passed, c.Detail }),
            issues      = r.Issues.Select(i => new { i.Severity, i.Category, i.Name, i.Message })
        };
        return JsonSerializer.Serialize(model, new JsonSerializerOptions { WriteIndented = true });
    }

    internal static string RenderHtml(ValidationReport r)
    {
        string statusClass = r.IsValid ? "valid" : "invalid";
        string statusText  = r.IsValid ? "VALID" : $"INVALID — {r.ErrorCount} error(s), {r.WarningCount} warning(s)";

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html lang=\"en\"><head>");
        sb.AppendLine("<meta charset=\"UTF-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        sb.AppendLine($"<title>whfmt validate — {r.FileName}</title>");
        sb.AppendLine(HtmlStyle());
        sb.AppendLine("</head><body>");
        sb.AppendLine("<div class=\"container\">");
        sb.AppendLine($"<h1>whfmt <span class=\"accent\">validate</span></h1>");
        sb.AppendLine($"<div class=\"meta\"><span class=\"label\">File</span> {Esc(r.FilePath)}</div>");
        sb.AppendLine($"<div class=\"meta\"><span class=\"label\">Size</span> {FormatSize(r.FileSize)}</div>");

        if (!r.FileNotFound)
        {
            sb.AppendLine($"<div class=\"meta\"><span class=\"label\">Format</span> {Esc(r.FormatName)} <span class=\"badge\">{Esc(r.FormatCategory)}</span></div>");
            sb.AppendLine($"<div class=\"meta\"><span class=\"label\">Confidence</span> {r.Confidence:P0} <span class=\"source\">[{r.MatchSource}]</span></div>");
            sb.AppendLine($"<div class=\"meta\"><span class=\"label\">Forensic Risk</span> <span class=\"risk-{r.ForensicRiskLevel.ToLower()}\">{r.ForensicRiskLevel.ToUpper()}</span></div>");
        }

        sb.AppendLine($"<div class=\"status {statusClass}\">{statusText}</div>");

        if (r.Checks.Count > 0)
        {
            sb.AppendLine("<h2>Passed Checks</h2><ul class=\"checks\">");
            foreach (var c in r.Checks)
                sb.AppendLine($"<li><span class=\"pass\">✓</span> <strong>[{Esc(c.Category)}]</strong> {Esc(c.Name)} <span class=\"detail\">— {Esc(c.Detail)}</span></li>");
            sb.AppendLine("</ul>");
        }

        if (r.Issues.Count > 0)
        {
            sb.AppendLine("<h2>Issues</h2><ul class=\"issues\">");
            foreach (var i in r.Issues)
            {
                string cls = i.Severity switch { "error" => "err", "warning" => "warn", _ => "info" };
                string icon = i.Severity switch { "error" => "✗", "warning" => "⚠", _ => "ℹ" };
                sb.AppendLine($"<li class=\"{cls}\"><span class=\"icon\">{icon}</span> <strong>[{Esc(i.Category)}]</strong> {Esc(i.Name)}: {Esc(i.Message)}</li>");
            }
            sb.AppendLine("</ul>");
        }

        sb.AppendLine($"<footer>Generated by <a href=\"https://github.com/abbaye/WpfHexEditorIDE\">whfmt.Validate</a> {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC</footer>");
        sb.AppendLine("</div></body></html>");
        return sb.ToString();
    }

    private static string HtmlStyle() => """
        <style>
        *{box-sizing:border-box;margin:0;padding:0}
        body{font-family:'Segoe UI',system-ui,sans-serif;background:#0d1117;color:#c9d1d9;padding:2rem}
        .container{max-width:860px;margin:0 auto}
        h1{font-size:1.8rem;margin-bottom:1.2rem;color:#58a6ff}
        h1 .accent{color:#f78166}
        h2{font-size:1.1rem;margin:1.4rem 0 .6rem;color:#8b949e;text-transform:uppercase;letter-spacing:.08em}
        .meta{margin:.3rem 0;font-size:.95rem}
        .label{color:#8b949e;min-width:100px;display:inline-block}
        .badge{background:#21262d;border:1px solid #30363d;border-radius:4px;padding:1px 6px;font-size:.8rem;margin-left:.4rem}
        .source{color:#8b949e;font-size:.85rem}
        .status{margin:1.2rem 0;padding:.7rem 1rem;border-radius:6px;font-weight:600;font-size:1rem}
        .status.valid{background:#0f2a1a;border:1px solid #2ea043;color:#3fb950}
        .status.invalid{background:#2a0f0f;border:1px solid #f85149;color:#f85149}
        .risk-low{color:#3fb950}.risk-medium{color:#d29922}.risk-high{color:#f85149}.risk-critical{color:#f85149;font-weight:700}
        ul{list-style:none;padding:0}
        ul li{padding:.4rem .6rem;border-radius:4px;margin:.2rem 0;font-size:.9rem}
        .checks li{background:#0f2a1a}
        .pass{color:#3fb950;margin-right:.4rem}
        .detail{color:#8b949e;font-size:.85rem}
        .issues li.err{background:#2a0f0f}
        .issues li.warn{background:#2a1e0a}
        .issues li.info{background:#0f1f2a}
        .icon{margin-right:.4rem}
        .err .icon{color:#f85149}.warn .icon{color:#d29922}.info .icon{color:#58a6ff}
        footer{margin-top:2rem;color:#484f58;font-size:.8rem}
        footer a{color:#58a6ff;text-decoration:none}
        </style>
        """;

    private static string Esc(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024        => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        _             => $"{bytes / 1024.0 / 1024.0:F2} MB"
    };
}
