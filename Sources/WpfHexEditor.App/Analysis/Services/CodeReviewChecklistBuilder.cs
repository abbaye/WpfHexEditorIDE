// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/Services/CodeReviewChecklistBuilder.cs
// Description: Generates a markdown code-review checklist from an analysis
//              report. Copy-paste into a PR description / review template.
//              No external dependencies — pure aggregation.
// ==========================================================

using System.Text;
using WpfHexEditor.App.Analysis.Models;

namespace WpfHexEditor.App.Analysis.Services;

internal static class CodeReviewChecklistBuilder
{
    internal static string Build(CodeAnalysisReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Code Review Checklist");
        sb.AppendLine();
        sb.AppendLine($"_Auto-generated from analysis run on {report.Timestamp:yyyy-MM-dd HH:mm}._");
        sb.AppendLine($"_Score: **{report.Score.Score}/100** ({report.Score.Grade})_  · _{report.TotalFiles} files · {report.TotalLines:N0} LOC_");
        sb.AppendLine();

        sb.AppendLine("## Quality Gates");
        sb.AppendLine($"- [ ] Score ≥ 70 (current: **{report.Score.Score}**)");
        sb.AppendLine($"- [ ] No regressions vs previous snapshot (delta: **{report.Score.TrendingDelta:+#;-#;0}**)");
        int errors   = 0, warnings = 0;
        foreach (var d in report.Diagnostics)
            if (d.Severity == DiagnosticSeverity.Error) errors++;
            else if (d.Severity == DiagnosticSeverity.Warning) warnings++;
        sb.AppendLine($"- [ ] 0 errors (current: **{errors}**), warnings reviewed (current: **{warnings}**)");
        if (report.ProjectCycles.Count > 0)
            sb.AppendLine($"- [ ] **{report.ProjectCycles.Count}** cyclic project dependency(ies) — see Coupling tab");
        sb.AppendLine();

        if (report.Score.WorstFiles.Count > 0)
        {
            sb.AppendLine("## Files needing attention");
            foreach (var f in report.Score.WorstFiles.Take(8))
            {
                string tags = f.IsHotspot ? " · 🔥 hotspot" : "";
                sb.AppendLine($"- [ ] `{f.FileName}` — score {f.Score}, LOC {f.TotalLines}, MaxCC {f.MaxCyclomaticComplexity}, MI {f.MaintainabilityIndex:F0}{tags}");
            }
            sb.AppendLine();
        }

        if (report.Duplications.Count > 0)
        {
            sb.AppendLine($"## Duplication ({report.Duplications.Count} clones)");
            sb.AppendLine($"- [ ] Review whether top duplicates can be extracted to shared helpers");
            sb.AppendLine();
        }

        if (report.DeadSymbols.Count > 0)
        {
            sb.AppendLine($"## Dead code ({report.DeadSymbols.Count} symbols)");
            sb.AppendLine($"- [ ] Confirm dead symbols are truly unused (not via reflection / generators)");
            sb.AppendLine();
        }

        var topRules = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var d in report.Diagnostics)
            topRules[d.Id] = topRules.GetValueOrDefault(d.Id) + 1;

        if (topRules.Count > 0)
        {
            sb.AppendLine("## Top rule violations");
            foreach (var (rule, count) in topRules.OrderByDescending(kv => kv.Value).Take(6))
                sb.AppendLine($"- [ ] {rule} — {count} occurrence(s)");
            sb.AppendLine();
        }

        sb.AppendLine("## Sign-off");
        sb.AppendLine("- [ ] Tests pass locally");
        sb.AppendLine("- [ ] Documentation updated where applicable");
        sb.AppendLine("- [ ] Public API changes are intentional & documented");
        return sb.ToString();
    }
}
