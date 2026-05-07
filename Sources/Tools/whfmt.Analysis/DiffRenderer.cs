// ==========================================================
// Project: whfmt.Analysis
// File: DiffRenderer.cs
// Description: Renders DiffResult to text, hex, JSON, CSV, Markdown, or HTML.
// Architecture: v1.1 — hex diff, checksum section, structural diff, CSV, Markdown added.
// ==========================================================

using System.Text;
using System.Text.Json;

namespace WhfmtAnalysis;

/// <summary>Renders a <see cref="DiffResult"/> to multiple output formats.</summary>
public static class DiffRenderer
{
    // ── Text ─────────────────────────────────────────────────────────────────

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

        var changed   = new List<FieldChange>();
        var unchanged = new List<FieldChange>();
        var ignored   = new List<FieldChange>();
        foreach (var f in r.FieldChanges)
        {
            if (f.IsIgnored)          ignored.Add(f);
            else if (f.IsChanged)     changed.Add(f);
            else                      unchanged.Add(f);
        }

        if (changed.Count > 0)
        {
            sb.AppendLine($"  Changed fields ({changed.Count}):");
            foreach (var c in changed)
            {
                string corr = c.IsCorrupted ? " [CORRUPTED CHECKSUM]" : "";
                sb.AppendLine($"    ≠  {c.FieldName,-30}  A: {c.ValueA}  →  B: {c.ValueB}{corr}");
                if (c.HexDiff is { } hd && hd.DifferentBytes > 0)
                    RenderHexDiffInline(sb, hd);
            }
            sb.AppendLine();
        }

        if (unchanged.Count > 0)
        {
            sb.AppendLine($"  Unchanged fields ({unchanged.Count}):");
            foreach (var c in unchanged) sb.AppendLine($"    =  {c.FieldName,-30}  {c.ValueA}");
            sb.AppendLine();
        }

        if (ignored.Count > 0)
        {
            sb.AppendLine($"  Ignored fields ({ignored.Count}):");
            foreach (var c in ignored) sb.AppendLine($"    ·  {c.FieldName,-30}  A: {c.ValueA}  /  B: {c.ValueB}");
            sb.AppendLine();
        }

        // Checksum section
        RenderChecksumsText(sb, r);

        // Structural diff section
        if (r.StructuralDiff is { } sd && (sd.OnlyInA.Count > 0 || sd.OnlyInB.Count > 0))
        {
            sb.AppendLine($"  Structural diff:");
            foreach (var b in sd.OnlyInA)  sb.AppendLine($"    - only in A: {b.Name} @ {b.Offset} ({b.Length} bytes)");
            foreach (var b in sd.OnlyInB)  sb.AppendLine($"    + only in B: {b.Name} @ {b.Offset} ({b.Length} bytes)");
            sb.AppendLine();
        }

        sb.AppendLine($"  Result: {changed.Count} change(s), {unchanged.Count} match(es), {ignored.Count} ignored");
        sb.AppendLine();
        return sb.ToString();
    }

    private static void RenderHexDiffInline(StringBuilder sb, HexDiff hd)
    {
        int show = Math.Min(16, Math.Max(hd.BytesA.Length, hd.BytesB.Length));
        sb.Append($"       Hex A: ");
        for (int i = 0; i < show; i++) sb.Append(i < hd.BytesA.Length ? $"{hd.BytesA[i]:X2} " : "   ");
        sb.AppendLine();
        sb.Append($"       Hex B: ");
        for (int i = 0; i < show; i++) sb.Append(i < hd.BytesB.Length ? $"{hd.BytesB[i]:X2} " : "   ");
        sb.AppendLine();
        sb.Append($"       Diff:  ");
        for (int i = 0; i < show; i++) sb.Append(i < hd.DiffMask.Length && hd.DiffMask[i] ? "^^ " : "   ");
        sb.AppendLine();
    }

    private static void RenderChecksumsText(StringBuilder sb, DiffResult r)
    {
        var allCs = r.ChecksumsA.Concat(r.ChecksumsB).ToList();
        if (allCs.Count == 0) return;
        sb.AppendLine("  Checksums:");
        foreach (var cs in r.ChecksumsA)
        {
            string mark = cs.IsValid ? "✓" : "✗";
            sb.AppendLine($"    A  {cs.Algorithm,-8} @ {cs.StoredOffset,-6}  {mark}  stored={cs.StoredHex}  computed={cs.ComputedHex}");
        }
        foreach (var cs in r.ChecksumsB)
        {
            string mark = cs.IsValid ? "✓" : "✗";
            sb.AppendLine($"    B  {cs.Algorithm,-8} @ {cs.StoredOffset,-6}  {mark}  stored={cs.StoredHex}  computed={cs.ComputedHex}");
        }
        sb.AppendLine();
    }

    // ── JSON ─────────────────────────────────────────────────────────────────

    /// <summary>Render as structured JSON — suitable for CI/CD pipelines.</summary>
    public static string RenderJson(DiffResult r)
    {
        var model = new
        {
            fileA          = r.FileA,
            fileB          = r.FileB,
            sizeA          = r.SizeA,
            sizeB          = r.SizeB,
            format         = r.FormatName,
            formatsMatch   = r.FormatsMatch,
            isIdentical    = r.IsIdentical,
            rawByteDelta   = r.RawByteDelta,
            changedCount   = r.ChangedCount,
            unchangedCount = r.UnchangedCount,
            error          = r.Error,
            fields         = r.FieldChanges.Select(f => new
            {
                f.FieldName, f.ValueA, f.ValueB, f.IsChanged, f.IsIgnored, f.IsCorrupted,
                hexDiff = f.HexDiff is null ? null : new
                {
                    offset        = f.HexDiff.Offset,
                    differentBytes= f.HexDiff.DifferentBytes,
                    hexA          = BitConverter.ToString(f.HexDiff.BytesA).Replace("-", " "),
                    hexB          = BitConverter.ToString(f.HexDiff.BytesB).Replace("-", " "),
                },
            }),
            checksumsA = r.ChecksumsA.Select(c => new { c.Algorithm, c.StoredOffset, c.StoredHex, c.ComputedHex, c.IsValid }),
            checksumsB = r.ChecksumsB.Select(c => new { c.Algorithm, c.StoredOffset, c.StoredHex, c.ComputedHex, c.IsValid }),
            structuralDiff = r.StructuralDiff is null ? null : new
            {
                onlyInA = r.StructuralDiff.OnlyInA.Select(b => new { b.Name, b.Offset, b.Length, b.Hash }),
                onlyInB = r.StructuralDiff.OnlyInB.Select(b => new { b.Name, b.Offset, b.Length, b.Hash }),
                inBoth  = r.StructuralDiff.InBoth.Count,
            },
        };
        return JsonSerializer.Serialize(model, new JsonSerializerOptions { WriteIndented = true });
    }

    // ── CSV ──────────────────────────────────────────────────────────────────

    /// <summary>Render as CSV — one row per field change, suitable for spreadsheet import.</summary>
    public static string ToCsv(DiffResult r)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Field,ValueA,ValueB,IsChanged,IsIgnored,IsCorrupted,DifferentBytes");
        foreach (var f in r.FieldChanges)
        {
            sb.AppendLine($"{CsvEsc(f.FieldName)},{CsvEsc(f.ValueA)},{CsvEsc(f.ValueB)},{f.IsChanged},{f.IsIgnored},{f.IsCorrupted},{f.HexDiff?.DifferentBytes ?? 0}");
        }
        return sb.ToString();
    }

    // ── Markdown ─────────────────────────────────────────────────────────────

    /// <summary>Render as GitHub Markdown table.</summary>
    public static string ToMarkdown(DiffResult r)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"## whfmt diff — `{r.FormatName}`");
        sb.AppendLine();
        sb.AppendLine($"| | File | Size |");
        sb.AppendLine($"|---|---|---|");
        sb.AppendLine($"| **A** | `{r.FileA}` | {FormatSize(r.SizeA)} |");
        sb.AppendLine($"| **B** | `{r.FileB}` | {FormatSize(r.SizeB)} |");
        sb.AppendLine();

        if (r.Error is not null) { sb.AppendLine($"> ⚠️ **Error:** {r.Error}"); return sb.ToString(); }
        if (r.IsIdentical)       { sb.AppendLine("> ✅ Files are semantically **IDENTICAL**."); return sb.ToString(); }

        sb.AppendLine($"**{r.ChangedCount} field(s) changed** · size Δ: `{(r.RawByteDelta >= 0 ? "+" : "")}{r.RawByteDelta}` bytes");
        sb.AppendLine();
        sb.AppendLine("| Field | Value A | Value B | Status |");
        sb.AppendLine("|---|---|---|---|");

        foreach (var f in r.FieldChanges)
        {
            string status = f.IsIgnored ? "⬜ ignored" : f.IsCorrupted ? "☠️ corrupted" : f.IsChanged ? "🔴 changed" : "🟢 same";
            sb.AppendLine($"| `{f.FieldName}` | `{MdEsc(f.ValueA)}` | `{MdEsc(f.ValueB)}` | {status} |");
        }

        if (r.ChecksumsA.Count > 0 || r.ChecksumsB.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("### Checksums");
            sb.AppendLine("| File | Algorithm | Offset | Stored | Computed | Valid |");
            sb.AppendLine("|---|---|---|---|---|---|");
            foreach (var cs in r.ChecksumsA)
                sb.AppendLine($"| A | {cs.Algorithm} | {cs.StoredOffset} | `{cs.StoredHex}` | `{cs.ComputedHex}` | {(cs.IsValid ? "✅" : "❌")} |");
            foreach (var cs in r.ChecksumsB)
                sb.AppendLine($"| B | {cs.Algorithm} | {cs.StoredOffset} | `{cs.StoredHex}` | `{cs.ComputedHex}` | {(cs.IsValid ? "✅" : "❌")} |");
        }

        if (r.StructuralDiff is { } sd && (sd.OnlyInA.Count > 0 || sd.OnlyInB.Count > 0))
        {
            sb.AppendLine();
            sb.AppendLine("### Structural diff");
            sb.AppendLine("| Side | Block | Offset | Length |");
            sb.AppendLine("|---|---|---|---|");
            foreach (var b in sd.OnlyInA) sb.AppendLine($"| A only | `{b.Name}` | {b.Offset} | {b.Length} |");
            foreach (var b in sd.OnlyInB) sb.AppendLine($"| B only | `{b.Name}` | {b.Offset} | {b.Length} |");
        }

        return sb.ToString();
    }

    // ── HTML ─────────────────────────────────────────────────────────────────

    /// <summary>Render as self-contained dark-themed HTML with hex diff + checksum + structural sections.</summary>
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
            sb.AppendLine("<h2>Fields</h2>");
            sb.AppendLine("<table><thead><tr><th>Field</th><th>Value A</th><th>Value B</th><th>Status</th></tr></thead><tbody>");
            foreach (var f in r.FieldChanges)
            {
                string cls    = f.IsIgnored ? "ignored" : f.IsCorrupted ? "corrupted" : f.IsChanged ? "changed" : "same";
                string status = f.IsIgnored ? "ignored" : f.IsCorrupted ? "☠ corrupted" : f.IsChanged ? "≠ changed" : "= same";
                sb.AppendLine($"<tr class=\"{cls}\"><td>{Esc(f.FieldName)}</td><td>{Esc(f.ValueA)}</td><td>{Esc(f.ValueB)}</td><td>{status}</td></tr>");
                if (f.HexDiff is { DifferentBytes: > 0 } hd)
                {
                    sb.AppendLine($"<tr class=\"hexrow\"><td colspan=\"4\">");
                    AppendHexHtml(sb, hd);
                    sb.AppendLine("</td></tr>");
                }
            }
            sb.AppendLine("</tbody></table>");
        }

        // Checksum section
        if (r.ChecksumsA.Count + r.ChecksumsB.Count > 0)
        {
            sb.AppendLine("<h2>Checksums</h2>");
            sb.AppendLine("<table><thead><tr><th>File</th><th>Algorithm</th><th>Offset</th><th>Stored</th><th>Computed</th><th>Valid</th></tr></thead><tbody>");
            foreach (var cs in r.ChecksumsA)
                sb.AppendLine($"<tr class=\"{(cs.IsValid ? "same" : "changed")}\"><td>A</td><td>{cs.Algorithm}</td><td>{cs.StoredOffset}</td><td><code>{cs.StoredHex}</code></td><td><code>{cs.ComputedHex}</code></td><td>{(cs.IsValid ? "✓" : "✗")}</td></tr>");
            foreach (var cs in r.ChecksumsB)
                sb.AppendLine($"<tr class=\"{(cs.IsValid ? "same" : "changed")}\"><td>B</td><td>{cs.Algorithm}</td><td>{cs.StoredOffset}</td><td><code>{cs.StoredHex}</code></td><td><code>{cs.ComputedHex}</code></td><td>{(cs.IsValid ? "✓" : "✗")}</td></tr>");
            sb.AppendLine("</tbody></table>");
        }

        // Structural diff
        if (r.StructuralDiff is { } sd2 && (sd2.OnlyInA.Count + sd2.OnlyInB.Count > 0))
        {
            sb.AppendLine("<h2>Structural Diff</h2>");
            sb.AppendLine("<table><thead><tr><th>Side</th><th>Block</th><th>Offset</th><th>Length</th></tr></thead><tbody>");
            foreach (var b in sd2.OnlyInA) sb.AppendLine($"<tr class=\"changed\"><td>A only</td><td>{Esc(b.Name)}</td><td>{b.Offset}</td><td>{b.Length}</td></tr>");
            foreach (var b in sd2.OnlyInB) sb.AppendLine($"<tr class=\"same\"><td>B only</td><td>{Esc(b.Name)}</td><td>{b.Offset}</td><td>{b.Length}</td></tr>");
            sb.AppendLine("</tbody></table>");
        }

        sb.AppendLine($"<footer>Generated by <a href=\"https://github.com/abbaye/WpfHexEditorIDE\">whfmt.Analysis</a> {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC</footer>");
        sb.AppendLine("</div></body></html>");
        return sb.ToString();
    }

    private static void AppendHexHtml(StringBuilder sb, HexDiff hd)
    {
        int show = Math.Min(32, Math.Max(hd.BytesA.Length, hd.BytesB.Length));
        sb.Append("<div class=\"hexdiff\"><div class=\"hexrow-label\">A:</div><div class=\"hexbytes\">");
        for (int i = 0; i < show; i++)
        {
            bool diff = i < hd.DiffMask.Length && hd.DiffMask[i];
            string hex = i < hd.BytesA.Length ? $"{hd.BytesA[i]:X2}" : "--";
            sb.Append(diff ? $"<span class=\"diffbyte\">{hex}</span>" : hex);
            sb.Append(' ');
        }
        sb.Append("</div><div class=\"hexrow-label\">B:</div><div class=\"hexbytes\">");
        for (int i = 0; i < show; i++)
        {
            bool diff = i < hd.DiffMask.Length && hd.DiffMask[i];
            string hex = i < hd.BytesB.Length ? $"{hd.BytesB[i]:X2}" : "--";
            sb.Append(diff ? $"<span class=\"diffbyte\">{hex}</span>" : hex);
            sb.Append(' ');
        }
        sb.Append("</div></div>");
    }

    private static string HtmlStyle() => """
        <style>
        *{box-sizing:border-box;margin:0;padding:0}
        body{font-family:'Segoe UI',system-ui,sans-serif;background:#0d1117;color:#c9d1d9;padding:2rem}
        .container{max-width:960px;margin:0 auto}
        h1{font-size:1.8rem;margin-bottom:1rem;color:#58a6ff}.accent{color:#f78166}
        h2{font-size:1.1rem;color:#8b949e;margin:1.4rem 0 .5rem}
        .meta{margin:.3rem 0;font-size:.9rem}.label{color:#8b949e;min-width:60px;display:inline-block}
        .size{color:#8b949e;font-size:.85rem;margin-left:.5rem}
        .status{margin:1rem 0;padding:.6rem 1rem;border-radius:6px;font-weight:600}
        .identical{background:#0f2a1a;border:1px solid #2ea043;color:#3fb950}
        .changed{background:#2a0f0f;border:1px solid #f85149;color:#f85149}
        .error{background:#2a1a0a;border:1px solid #d29922;color:#d29922;padding:.6rem 1rem;border-radius:6px;margin:1rem 0}
        table{width:100%;border-collapse:collapse;margin-top:.5rem;font-size:.88rem}
        th{background:#161b22;padding:.5rem .8rem;text-align:left;border:1px solid #30363d;color:#8b949e}
        td{padding:.4rem .8rem;border:1px solid #21262d;font-family:monospace}
        tr.changed td{background:#1a0a0a}tr.changed td:nth-child(2){color:#f85149}tr.changed td:nth-child(3){color:#3fb950}
        tr.corrupted td{background:#1a0a1a;color:#f78166}
        tr.ignored td{color:#484f58;font-style:italic}
        tr.hexrow td{background:#0d1117;padding:.2rem .8rem}
        .hexdiff{display:flex;gap:.5rem;align-items:baseline;font-family:monospace;font-size:.8rem;flex-wrap:wrap}
        .hexrow-label{color:#8b949e;min-width:1.8rem}
        .hexbytes{letter-spacing:.05rem}.diffbyte{color:#f85149;font-weight:700}
        code{background:#161b22;padding:.1rem .3rem;border-radius:3px}
        footer{margin-top:2rem;color:#484f58;font-size:.8rem}footer a{color:#58a6ff;text-decoration:none}
        </style>
        """;

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string Esc(string s)    => s.Replace("&","&amp;").Replace("<","&lt;").Replace(">","&gt;");
    private static string CsvEsc(string s) => s.Contains(',') || s.Contains('"') || s.Contains('\n') ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
    private static string MdEsc(string s)  => s.Replace("|", "\\|").Replace("`", "'");
    private static string FormatSize(long b) => b < 1024 ? $"{b} B" : b < 1048576 ? $"{b/1024.0:F1} KB" : $"{b/1048576.0:F2} MB";
}
