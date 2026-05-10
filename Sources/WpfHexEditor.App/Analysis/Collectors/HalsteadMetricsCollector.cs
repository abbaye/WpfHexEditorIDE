// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/Collectors/HalsteadMetricsCollector.cs
// Description: Halstead software science metrics — Volume, Difficulty, Effort, Bugs.
//              Operators = syntax tokens that act on operands (keywords, operators, punctuation).
//              Operands = identifiers + literals.
// Architecture Notes:
//     Stateless. Designed for parallel use. One pass over the method body.
// ==========================================================

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace WpfHexEditor.App.Analysis.Collectors;

internal static class HalsteadMetricsCollector
{
    public readonly record struct HalsteadResult(
        int    UniqueOperators, int Operators,
        int    UniqueOperands,  int Operands,
        double Volume, double Difficulty, double Effort, double Bugs);

    public static HalsteadResult Compute(SyntaxNode body)
    {
        var operators = new HashSet<string>(StringComparer.Ordinal);
        var operands  = new HashSet<string>(StringComparer.Ordinal);
        int totalOperators = 0;
        int totalOperands  = 0;

        foreach (var token in body.DescendantTokens())
        {
            var kind = token.Kind();
            if (IsOperand(kind))
            {
                operands.Add(token.ValueText);
                totalOperands++;
            }
            else if (IsOperator(kind))
            {
                operators.Add(token.Text);
                totalOperators++;
            }
        }

        int n1 = operators.Count;
        int n2 = operands.Count;
        int N1 = totalOperators;
        int N2 = totalOperands;

        if (n1 == 0 || n2 == 0)
            return new HalsteadResult(n1, N1, n2, N2, 0, 0, 0, 0);

        int    vocabulary = n1 + n2;
        int    length     = N1 + N2;
        double volume     = length * Math.Log2(vocabulary);
        double difficulty = (n1 / 2.0) * (N2 / (double)n2);
        double effort     = volume * difficulty;
        double bugs       = volume / 3000.0;

        return new HalsteadResult(n1, N1, n2, N2, volume, difficulty, effort, bugs);
    }

    private static bool IsOperand(SyntaxKind kind) => kind switch
    {
        SyntaxKind.IdentifierToken           => true,
        SyntaxKind.NumericLiteralToken       => true,
        SyntaxKind.StringLiteralToken        => true,
        SyntaxKind.CharacterLiteralToken     => true,
        SyntaxKind.InterpolatedStringTextToken => true,
        SyntaxKind.TrueKeyword               => true,
        SyntaxKind.FalseKeyword              => true,
        SyntaxKind.NullKeyword               => true,
        _                                    => false,
    };

    private static bool IsOperator(SyntaxKind kind)
    {
        // Treat keywords + symbolic operators as operators; skip whitespace / structural tokens
        if (kind == SyntaxKind.None) return false;
        if (kind == SyntaxKind.EndOfFileToken) return false;
        if (SyntaxFacts.IsTrivia(kind)) return false;
        if (SyntaxFacts.IsKeywordKind(kind))   return true;
        if (SyntaxFacts.IsPunctuation(kind))   return true;
        return false;
    }
}
