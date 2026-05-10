// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/Suppressions/SuppressionApplyService.cs
// Description: Applies a suppression decision to its canonical artifact —
//              source comment, file header, baseline json, or options json —
//              then triggers a re-run so the report refreshes.
// Architecture Notes:
//     One class = four short Apply* methods (≤25 lines each). Re-run is
//     invoked through a Func<Task> callback owned by CodeAnalysisModule.
// ==========================================================

using WpfHexEditor.App.Analysis.Models;
using WpfHexEditor.App.Analysis.Services;
using WpfHexEditor.App.Analysis.UI.ViewModels;

namespace WpfHexEditor.App.Analysis.Suppressions;

internal sealed class SuppressionApplyService
{
    private readonly CodeAnalysisOptionsService _options;
    private readonly AnalysisBaselineService    _baseline;
    private readonly Func<Task>?                _reRun;

    internal SuppressionApplyService(
        CodeAnalysisOptionsService options,
        AnalysisBaselineService    baseline,
        Func<Task>?                reRun)
    {
        _options  = options;
        _baseline = baseline;
        _reRun    = reRun;
    }

    /// <summary>Apply suppression for `issue` using `mode`. Returns true on success.</summary>
    internal async Task<bool> ApplyAsync(IssueViewModel issue, SuppressionMode mode)
    {
        bool ok = mode switch
        {
            SuppressionMode.InSource    => ApplyInSource(issue),
            SuppressionMode.InFile      => ApplyInFile(issue),
            SuppressionMode.InBaseline  => ApplyInBaseline(issue),
            SuppressionMode.DisableRule => ApplyDisableRule(issue),
            _ => false,
        };
        if (ok && _reRun is not null) await _reRun().ConfigureAwait(false);
        return ok;
    }

    private static bool ApplyInSource(IssueViewModel i)
        => InlineSuppressionWriter.WriteInSource(i.FilePath, i.Line, i.Id);

    private static bool ApplyInFile(IssueViewModel i)
        => InlineSuppressionWriter.WriteInFile(i.FilePath, i.Id);

    private bool ApplyInBaseline(IssueViewModel i)
    {
        var existing = _baseline.Load();
        bool alreadyPresent = existing.Any(e => e.RuleId == i.Id
                                             && string.Equals(e.FilePath, i.FilePath, StringComparison.OrdinalIgnoreCase)
                                             && Math.Abs(e.Line - i.Line) <= 2);
        if (alreadyPresent) { _options.Options.BaselineEnabled = true; _options.Save(); return true; }

        var diagnostics = existing.Select(EntryToDiag).ToList();
        diagnostics.Add(new AnalysisDiagnostic { Id = i.Id, FilePath = i.FilePath, Line = i.Line });
        _baseline.Save(diagnostics);
        _options.Options.BaselineEnabled = true;
        _options.Save();
        return true;
    }

    private static AnalysisDiagnostic EntryToDiag(AnalysisBaselineService.BaselineEntry e)
        => new() { Id = e.RuleId, FilePath = e.FilePath, Line = e.Line };

    private bool ApplyDisableRule(IssueViewModel i)
    {
        var rule = _options.Options.GetRule(i.Id);
        if (rule is null) return false;
        rule.Severity = RuleSeverity.Disabled;
        _options.Save();
        return true;
    }
}
