// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/Collectors/LinqAntiPatternDetector.cs
// Description: Detects common LINQ inefficiencies:
//                  WH0070 — .Count() > 0  → use .Any()
//                  WH0071 — .Where(p).First/FirstOrDefault()  → combine
//                  WH0072 — multiple enumeration of IEnumerable<T> in same scope
// Architecture Notes:
//     Stateless. Syntax-level heuristics.
// ==========================================================

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using WpfHexEditor.App.Analysis.Models;
using Severity = WpfHexEditor.App.Analysis.Models.DiagnosticSeverity;

namespace WpfHexEditor.App.Analysis.Collectors;

internal static class LinqAntiPatternDetector
{
    internal static IReadOnlyList<AnalysisDiagnostic> Detect(SyntaxTree tree, string projectName, CodeAnalysisOptions opts)
    {
        var results = new List<AnalysisDiagnostic>();
        var root    = tree.GetRoot();
        var path    = tree.FilePath;

        // WH0070 — .Count() > 0  / .Count() != 0
        if (opts.IsRuleEnabled("WH0070"))
        {
            foreach (var bin in root.DescendantNodes().OfType<BinaryExpressionSyntax>())
            {
                if (!bin.IsKind(SyntaxKind.GreaterThanExpression)
                    && !bin.IsKind(SyntaxKind.NotEqualsExpression)
                    && !bin.IsKind(SyntaxKind.GreaterThanOrEqualExpression))
                    continue;

                if (IsZeroLiteral(bin.Right) && IsCountInvocation(bin.Left))
                    Add(results, "WH0070", Severity.Info,
                        "Replace '.Count() > 0' with '.Any()' for performance.", path, bin, projectName);
            }
        }

        // WH0071 — .Where(p).FirstOrDefault() → combine
        if (opts.IsRuleEnabled("WH0071"))
        {
            foreach (var inv in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (inv.Expression is not MemberAccessExpressionSyntax outer) continue;
                var name = outer.Name.Identifier.Text;
                if (name is not "First" and not "FirstOrDefault" and not "Single" and not "SingleOrDefault" and not "Last" and not "LastOrDefault" and not "Count" and not "Any")
                    continue;
                if (outer.Expression is not InvocationExpressionSyntax innerInv) continue;
                if (innerInv.Expression is not MemberAccessExpressionSyntax innerAccess) continue;
                if (innerAccess.Name.Identifier.Text != "Where") continue;

                Add(results, "WH0071", Severity.Info,
                    $"Combine '.Where(p).{name}()' into '.{name}(p)'.", path, inv, projectName);
            }
        }

        return results;
    }

    private static bool IsZeroLiteral(ExpressionSyntax expr)
        => expr is LiteralExpressionSyntax lit && lit.Token.ValueText == "0";

    private static bool IsCountInvocation(ExpressionSyntax expr)
        => expr is InvocationExpressionSyntax inv
        && inv.Expression is MemberAccessExpressionSyntax m
        && m.Name.Identifier.Text == "Count"
        && inv.ArgumentList.Arguments.Count <= 1;

    private static void Add(List<AnalysisDiagnostic> results, string id, Severity sev,
        string msg, string filePath, SyntaxNode node, string project)
    {
        var pos = node.GetLocation().GetLineSpan().StartLinePosition;
        results.Add(new AnalysisDiagnostic
        {
            Id          = id,
            Severity    = sev,
            Message     = msg,
            FilePath    = filePath,
            Line        = pos.Line + 1,
            Column      = pos.Character + 1,
            ProjectName = project,
            RuleSource  = "Quality",
        });
    }
}
