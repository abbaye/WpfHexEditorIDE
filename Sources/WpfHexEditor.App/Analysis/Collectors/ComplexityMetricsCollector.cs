// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/Collectors/ComplexityMetricsCollector.cs
// Description: Computes Cyclomatic Complexity (McCabe) and an approximation of
//              Cognitive Complexity (nesting-weighted) for every method in a tree.
//              Stateless — safe for parallel use.
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

            results.Add(new MethodMetrics
            {
                Name                 = method.Identifier.Text,
                FullyQualifiedName   = BuildFqn(method),
                Line                 = loc.StartLinePosition.Line + 1,
                Loc                  = loc.EndLinePosition.Line - loc.StartLinePosition.Line + 1,
                CyclomaticComplexity = cc,
                CognitiveComplexity  = cog,
                ParameterCount       = method.ParameterList.Parameters.Count,
            });
        }

        return results;
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

    // ── Cognitive complexity (nesting-depth weighted) ─────────────────────────

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
                // Non-nesting increments: logical operators, ternary, null-coalesce
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
