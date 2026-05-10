// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/Collectors/ComplexityMetricsCollector.cs
// Description: Per-method metrics: Cyclomatic, Cognitive (Sonar-style),
//              Halstead suite, Maintainability Index (MS standard).
// Architecture Notes:
//     Stateless. Designed for parallel use.
// ==========================================================

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using WpfHexEditor.App.Analysis.Models;

namespace WpfHexEditor.App.Analysis.Collectors;

internal static class ComplexityMetricsCollector
{
    internal static IReadOnlyList<MethodMetrics> Collect(SyntaxTree tree)
    {
        var root    = tree.GetRoot();
        var results = new List<MethodMetrics>();

        foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            var cc  = ComputeCyclomatic(method);
            var cog = ComputeCognitive(method);
            var loc = method.GetLocation().GetLineSpan();
            int methodLoc = loc.EndLinePosition.Line - loc.StartLinePosition.Line + 1;

            // Halstead on the method body
            SyntaxNode body = (SyntaxNode?)method.Body
                              ?? (SyntaxNode?)method.ExpressionBody
                              ?? method;
            var hs = HalsteadMetricsCollector.Compute(body);

            // Maintainability Index (MS standard)
            // MI = max(0, (171 - 5.2*ln(V) - 0.23*CC - 16.2*ln(LOC) + 50*sin(sqrt(2.4*commentRatio))) * 100/171)
            double mi = ComputeMaintainabilityIndex(hs.Volume, cc, methodLoc, commentRatio: 0);

            results.Add(new MethodMetrics
            {
                Name                 = method.Identifier.Text,
                FullyQualifiedName   = BuildFqn(method),
                Line                 = loc.StartLinePosition.Line + 1,
                Loc                  = methodLoc,
                CyclomaticComplexity = cc,
                CognitiveComplexity  = cog,
                ParameterCount       = method.ParameterList.Parameters.Count,

                HalsteadOperators        = hs.Operators,
                HalsteadOperands         = hs.Operands,
                HalsteadUniqueOperators  = hs.UniqueOperators,
                HalsteadUniqueOperands   = hs.UniqueOperands,
                HalsteadVolume           = Math.Round(hs.Volume,     2),
                HalsteadDifficulty       = Math.Round(hs.Difficulty, 2),
                HalsteadEffort           = Math.Round(hs.Effort,     2),
                HalsteadBugs             = Math.Round(hs.Bugs,       3),

                MaintainabilityIndex = Math.Round(mi, 1),
            });
        }

        return results;
    }

    // ── Microsoft Maintainability Index (normalized 0-100) ────────────────────

    internal static double ComputeMaintainabilityIndex(double halsteadVolume, int cc, int loc, double commentRatio)
    {
        if (halsteadVolume <= 0 || loc <= 0) return 100;

        double raw = 171
                   - 5.2 * Math.Log(Math.Max(1, halsteadVolume))
                   - 0.23 * cc
                   - 16.2 * Math.Log(loc)
                   + 50 * Math.Sin(Math.Sqrt(2.4 * commentRatio));

        return Math.Clamp(raw * 100.0 / 171.0, 0, 100);
    }

    // ── McCabe cyclomatic complexity ──────────────────────────────────────────
    // CC = number of decision points + 1

    private static int ComputeCyclomatic(MethodDeclarationSyntax method)
    {
        int count = 1;
        foreach (var node in method.DescendantNodes())
        {
            count += node.Kind() switch
            {
                SyntaxKind.IfStatement            => 1,
                SyntaxKind.ElseClause             => 1,
                SyntaxKind.ForStatement           => 1,
                SyntaxKind.ForEachStatement       => 1,
                SyntaxKind.WhileStatement         => 1,
                SyntaxKind.DoStatement            => 1,
                SyntaxKind.CaseSwitchLabel        => 1,
                SyntaxKind.CasePatternSwitchLabel => 1,
                SyntaxKind.ConditionalExpression  => 1,
                SyntaxKind.CoalesceExpression     => 1,
                SyntaxKind.LogicalAndExpression   => 1,
                SyntaxKind.LogicalOrExpression    => 1,
                SyntaxKind.CatchClause            => 1,
                _                                 => 0,
            };
        }
        return count;
    }

    // ── Cognitive complexity (Sonar-style: nesting weighted) ──────────────────

    private static int ComputeCognitive(MethodDeclarationSyntax method)
    {
        int score = 0;
        ComputeCognitiveRecursive(method.Body ?? (SyntaxNode?)method.ExpressionBody ?? method, 0, ref score);
        return score;
    }

    private static void ComputeCognitiveRecursive(SyntaxNode node, int depth, ref int score)
    {
        foreach (var child in node.ChildNodes())
        {
            bool isNesting = child is IfStatementSyntax or ForStatementSyntax
                or ForEachStatementSyntax or WhileStatementSyntax or DoStatementSyntax
                or SwitchStatementSyntax or TryStatementSyntax;

            if (isNesting)
            {
                score += 1 + depth;
                ComputeCognitiveRecursive(child, depth + 1, ref score);
            }
            else
            {
                if (child is BinaryExpressionSyntax bin &&
                    (bin.IsKind(SyntaxKind.LogicalAndExpression) || bin.IsKind(SyntaxKind.LogicalOrExpression)))
                    score++;
                else if (child is ConditionalExpressionSyntax or ConditionalAccessExpressionSyntax)
                    score++;

                ComputeCognitiveRecursive(child, depth, ref score);
            }
        }
    }

    private static string BuildFqn(MethodDeclarationSyntax method)
    {
        var type = method.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();
        return type is null
            ? method.Identifier.Text
            : $"{type.Identifier.Text}.{method.Identifier.Text}";
    }
}
