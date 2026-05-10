// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/Services/QualityGateEvaluator.cs
// Description: Evaluates a report against user-configured quality-gate
//              thresholds. Returns a pass/fail with the failed conditions.
// Architecture Notes:
//     Stateless. Pure function: report + gate → result.
// ==========================================================

using WpfHexEditor.App.Analysis.Models;

namespace WpfHexEditor.App.Analysis.Services;

internal static class QualityGateEvaluator
{
    public sealed record Result(bool Passed, IReadOnlyList<string> Failures);

    internal static Result Evaluate(CodeAnalysisReport report, CodeAnalysisOptions opts)
    {
        var failures = new List<string>();

        if (opts.QualityGateEnabled)
        {
            if (report.Score.Score < opts.QualityGateMinScore)
                failures.Add($"Score {report.Score.Score} < min {opts.QualityGateMinScore}");

            if (report.Score.TrendingDelta < opts.QualityGateMaxNegativeDelta)
                failures.Add($"Score delta {report.Score.TrendingDelta} < {opts.QualityGateMaxNegativeDelta}");

            int errors = report.Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error);
            if (errors > opts.QualityGateMaxErrors)
                failures.Add($"Errors {errors} > {opts.QualityGateMaxErrors}");
        }

        return new Result(failures.Count == 0, failures);
    }
}
