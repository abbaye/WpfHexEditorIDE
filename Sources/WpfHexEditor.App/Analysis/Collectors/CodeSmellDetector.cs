// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/Collectors/CodeSmellDetector.cs
// Description: Detects code smells across the whole compilation:
//                  WH0043 — Feature Envy (method touches another type's state more than its own)
//                  WH0044 — Data Clumps (≥3 parameter groups repeated across ≥3 sites)
// Architecture Notes:
//     Compilation-scoped (needs to see all methods together).
//     Heuristic — designed to avoid false-positives on common patterns.
// ==========================================================

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using WpfHexEditor.App.Analysis.Models;
using Severity = WpfHexEditor.App.Analysis.Models.DiagnosticSeverity;

namespace WpfHexEditor.App.Analysis.Collectors;

internal static class CodeSmellDetector
{
    internal static IReadOnlyList<AnalysisDiagnostic> Detect(IReadOnlyList<SyntaxTree> trees, CodeAnalysisOptions opts, string projectName)
    {
        var results = new List<AnalysisDiagnostic>();

        if (opts.IsRuleEnabled("WH0044"))
            DetectDataClumps(trees, results, projectName);

        if (opts.IsRuleEnabled("WH0043"))
            DetectFeatureEnvy(trees, results, projectName, opts);

        return results;
    }

    // ── WH0043 — Feature Envy ─────────────────────────────────────────────────
    // Heuristic: a method is "envious" when it accesses members of a foreign
    // receiver more often than its own (this/self) members.
    //
    //   envyRatio = foreignAccess / max(1, selfAccess)
    //
    // We only flag when foreign access dominates AND the absolute count is
    // meaningful (≥ 6), to avoid spam on tiny accessors.

    private const int FeatureEnvyMinForeign = 6;
    private const double FeatureEnvyRatio   = 2.0;

    private static void DetectFeatureEnvy(IReadOnlyList<SyntaxTree> trees, List<AnalysisDiagnostic> results, string projectName, CodeAnalysisOptions opts)
    {
        foreach (var tree in trees)
        {
            foreach (var method in tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                int self = 0, foreign = 0;
                string? topReceiver = null;
                int topReceiverCount = 0;
                var receiverCounts = new Dictionary<string, int>(StringComparer.Ordinal);

                foreach (var ma in method.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
                {
                    var receiverText = ma.Expression.ToString();
                    if (receiverText == "this" || receiverText == "base")
                    {
                        self++;
                    }
                    else
                    {
                        foreign++;
                        receiverCounts[receiverText] = receiverCounts.GetValueOrDefault(receiverText) + 1;
                        if (receiverCounts[receiverText] > topReceiverCount)
                        {
                            topReceiverCount = receiverCounts[receiverText];
                            topReceiver = receiverText;
                        }
                    }
                }

                if (foreign < FeatureEnvyMinForeign) continue;
                if (foreign < self * FeatureEnvyRatio) continue;

                var pos = method.Identifier.GetLocation().GetLineSpan().StartLinePosition;
                results.Add(new AnalysisDiagnostic
                {
                    Id          = "WH0043",
                    Severity    = Severity.Info,
                    Message     = $"Feature envy: '{method.Identifier.Text}' touches '{topReceiver}' {topReceiverCount}× vs self {self}× — consider moving to '{topReceiver}'.",
                    FilePath    = tree.FilePath,
                    Line        = pos.Line + 1,
                    Column      = pos.Character + 1,
                    ProjectName = projectName,
                    RuleSource  = "Quality",
                });
            }
        }
    }

    // ── WH0044 — Data Clumps ──────────────────────────────────────────────────
    // A "clump" is a sorted tuple of ≥3 parameter type names. If the same clump
    // appears in ≥3 distinct methods, suggest extracting a record/class.

    private static void DetectDataClumps(IReadOnlyList<SyntaxTree> trees, List<AnalysisDiagnostic> results, string projectName)
    {
        var clumps = new Dictionary<string, List<(string FilePath, int Line, string Method)>>(StringComparer.Ordinal);

        foreach (var tree in trees)
        {
            foreach (var method in tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                var ps = method.ParameterList.Parameters;
                if (ps.Count < 3) continue;

                var clump = string.Join(",", ps
                    .Select(p => p.Type?.ToString() ?? "?")
                    .OrderBy(x => x, StringComparer.Ordinal));

                if (!clumps.TryGetValue(clump, out var list))
                    clumps[clump] = list = [];

                var pos = method.Identifier.GetLocation().GetLineSpan().StartLinePosition;
                list.Add((tree.FilePath, pos.Line + 1, method.Identifier.Text));
            }
        }

        foreach (var (clump, sites) in clumps)
        {
            if (sites.Count < 3) continue;
            foreach (var (file, line, name) in sites)
                results.Add(new AnalysisDiagnostic
                {
                    Id          = "WH0044",
                    Severity    = Severity.Info,
                    Message     = $"Data clump: {sites.Count} methods share params [{clump}] — consider extracting a record.",
                    FilePath    = file,
                    Line        = line,
                    Column      = 1,
                    ProjectName = projectName,
                    RuleSource  = "Quality",
                });
        }
    }
}
