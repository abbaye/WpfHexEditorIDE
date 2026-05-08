// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/Collectors/AsyncAntiPatternDetector.cs
// Description: Detects common async/await pitfalls:
//                  WH0060 — .Result / .Wait() blocking calls
//                  WH0061 — async void (non event-handler)
//                  WH0062 — missing ConfigureAwait(false) in library code
// Architecture Notes:
//     Stateless. Syntax-only (no SemanticModel) — heuristic but fast.
// ==========================================================

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using WpfHexEditor.App.Analysis.Models;
using Severity = WpfHexEditor.App.Analysis.Models.DiagnosticSeverity;

namespace WpfHexEditor.App.Analysis.Collectors;

internal static class AsyncAntiPatternDetector
{
    internal static IReadOnlyList<AnalysisDiagnostic> Detect(SyntaxTree tree, string projectName, CodeAnalysisOptions opts)
    {
        var results = new List<AnalysisDiagnostic>();
        var root    = tree.GetRoot();
        var path    = tree.FilePath;

        // WH0060 — only flag patterns that are *certainly* async-blocking:
        //   - .Wait() invocation
        //   - .GetAwaiter().GetResult() chain
        // Plain ".Result" is too noisy without a semantic model (matches HttpResponseMessage.Result,
        // IdentityResult.Result, OperationResult.Result, …) so we skip it for now.
        if (opts.IsRuleEnabled("WH0060"))
        {
            foreach (var inv in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (inv.Expression is not MemberAccessExpressionSyntax access) continue;
                var name = access.Name.Identifier.Text;

                bool isWait        = name == "Wait" && inv.ArgumentList.Arguments.Count == 0;
                bool isGetResult   = name == "GetResult"
                                  && access.Expression is InvocationExpressionSyntax inner
                                  && inner.Expression is MemberAccessExpressionSyntax innerAccess
                                  && innerAccess.Name.Identifier.Text == "GetAwaiter";

                if (!isWait && !isGetResult) continue;

                var pos = inv.GetLocation().GetLineSpan().StartLinePosition;
                results.Add(Diag("WH0060", Severity.Warning,
                    isWait ? "Blocking on async via '.Wait()' — risk of deadlock; use 'await'."
                           : "Blocking on async via '.GetAwaiter().GetResult()' — use 'await'.",
                    path, pos.Line + 1, pos.Character + 1, projectName));
            }
        }

        // WH0061 — async void (skip event handlers: signature with sender + EventArgs)
        if (opts.IsRuleEnabled("WH0061"))
        {
            foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                bool isAsyncVoid = method.Modifiers.Any(m => m.IsKind(SyntaxKind.AsyncKeyword))
                                && method.ReturnType is PredefinedTypeSyntax pt
                                && pt.Keyword.IsKind(SyntaxKind.VoidKeyword);
                if (!isAsyncVoid) continue;

                if (LooksLikeEventHandler(method)) continue;

                var pos = method.Identifier.GetLocation().GetLineSpan().StartLinePosition;
                results.Add(Diag("WH0061", Severity.Warning,
                    $"Method '{method.Identifier.Text}' is async void — exceptions cannot be caught.",
                    path, pos.Line + 1, pos.Character + 1, projectName));
            }
        }

        // WH0062 — missing ConfigureAwait(false). Skip in obvious app/UI code.
        // Heuristic: only flag in files that don't reference WPF/WinForms namespaces.
        if (opts.IsRuleEnabled("WH0062") && !LooksLikeUiCode(root))
        {
            foreach (var awaitExpr in root.DescendantNodes().OfType<AwaitExpressionSyntax>())
            {
                if (HasConfigureAwait(awaitExpr)) continue;
                var pos = awaitExpr.GetLocation().GetLineSpan().StartLinePosition;
                results.Add(Diag("WH0062", Severity.Info,
                    "Consider 'ConfigureAwait(false)' for library code.",
                    path, pos.Line + 1, pos.Character + 1, projectName));
            }
        }

        return results;
    }

    private static bool LooksLikeEventHandler(MethodDeclarationSyntax method)
    {
        var ps = method.ParameterList.Parameters;
        if (ps.Count != 2) return false;
        var second = ps[1].Type?.ToString() ?? string.Empty;
        return second.EndsWith("EventArgs", StringComparison.Ordinal)
            || second.EndsWith("Args",      StringComparison.Ordinal);
    }

    private static bool LooksLikeUiCode(SyntaxNode root)
    {
        foreach (var u in root.DescendantNodes().OfType<UsingDirectiveSyntax>())
        {
            var s = u.Name?.ToString() ?? string.Empty;
            if (s.StartsWith("System.Windows", StringComparison.Ordinal)) return true;
            if (s.StartsWith("System.Windows.Forms", StringComparison.Ordinal)) return true;
        }
        return false;
    }

    private static bool HasConfigureAwait(AwaitExpressionSyntax awaitExpr)
    {
        return awaitExpr.Expression is InvocationExpressionSyntax inv
            && inv.Expression is MemberAccessExpressionSyntax m
            && m.Name.Identifier.Text == "ConfigureAwait";
    }

    private static AnalysisDiagnostic Diag(string id, Severity sev, string msg,
        string filePath, int line, int col, string project) => new()
    {
        Id          = id,
        Severity    = sev,
        Message     = msg,
        FilePath    = filePath,
        Line        = line,
        Column      = col,
        ProjectName = project,
        RuleSource  = "Quality",
    };
}
