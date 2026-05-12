// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/Services/DuplicationReportExporter.cs
// Description: Markdown export focused on the Duplication tab — used by the
//              "Export clones report" toolbar button.
// ==========================================================

using System.Text;
using WpfHexEditor.App.Analysis.UI.ViewModels;
using WpfHexEditor.App.Properties;

namespace WpfHexEditor.App.Analysis.Services;

internal static class DuplicationReportExporter
{
    internal static string Build(CodeAnalysisReportViewModel vm)
    {
        var sb = new StringBuilder();
        sb.AppendLine(AppResources.CodeAnalysis_Duplication_Export_Title);
        sb.AppendLine();
        sb.AppendLine($"> {string.Format(AppResources.CodeAnalysis_Duplication_Summary, vm.DuplicationGroups.Count, vm.TotalDuplicatedLines, vm.DuplicationRatioPercent)}");
        sb.AppendLine();

        int n = 0;
        foreach (var d in vm.DuplicationGroups)
        {
            n++;
            sb.AppendLine($"## #{n} — {d.LineCount} lines · {d.OccurrenceCount} occurrences · {d.TokenCount} tokens · `{d.Severity}`");
            foreach (var o in d.Occurrences)
                sb.AppendLine($"- `{o.FilePath}` : {o.StartLine}–{o.EndLine}");
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
