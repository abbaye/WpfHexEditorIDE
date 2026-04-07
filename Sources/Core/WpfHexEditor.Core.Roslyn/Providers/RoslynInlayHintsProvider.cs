// ==========================================================
// Project: WpfHexEditor.Core.Roslyn
// File: Providers/RoslynInlayHintsProvider.cs
// Contributors: Claude Opus 4.6, Claude Sonnet 4.6
// Created: 2026-04-01
// Updated: 2026-04-07 — var type inference hints + lambda return type hints
// Description:
//     Inlay hints for C# and VB.NET:
//       - Parameter name hints (method arguments)
//       - var type inference hints (e.g. var x = 1 → : int)
//       - Lambda/anonymous function return type hints
// ==========================================================

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using WpfHexEditor.Editor.Core.LSP;

namespace WpfHexEditor.Core.Roslyn.Providers;

internal static class RoslynInlayHintsProvider
{
    public static async Task<IReadOnlyList<LspInlayHint>> GetInlayHintsAsync(
        Document document, int startLine, int endLine, CancellationToken ct,
        bool showVarTypeHints = true, bool showLambdaReturnTypeHints = true)
    {
        var root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
        var semanticModel = await document.GetSemanticModelAsync(ct).ConfigureAwait(false);
        var text = await document.GetTextAsync(ct).ConfigureAwait(false);
        if (root is null || semanticModel is null) return [];

        var startPos = text.Lines[startLine].Start;
        var endPos = text.Lines[Math.Min(endLine, text.Lines.Count - 1)].End;
        var span = TextSpan.FromBounds(startPos, endPos);

        var results = new List<LspInlayHint>();

        if (document.Project.Language == LanguageNames.CSharp)
        {
            CollectCSharpHints(root, semanticModel, span, text, results, ct);
            if (showVarTypeHints) CollectVarTypeHints(root, semanticModel, span, text, results, ct);
            if (showLambdaReturnTypeHints) CollectLambdaReturnTypeHints(root, semanticModel, span, text, results, ct);
        }
        else if (document.Project.Language == LanguageNames.VisualBasic)
            CollectVbHints(root, semanticModel, span, text, results, ct);

        return results;
    }

    private static void CollectCSharpHints(
        SyntaxNode root, SemanticModel model, TextSpan span, SourceText text,
        List<LspInlayHint> results, CancellationToken ct)
    {
        foreach (var node in root.DescendantNodes(span))
        {
            if (node is not ArgumentSyntax arg) continue;
            if (arg.NameColon is not null) continue; // Already named.
            if (arg.Parent is not ArgumentListSyntax argList) continue;

            var invocation = argList.Parent;
            if (invocation is null) continue;

            var symbolInfo = model.GetSymbolInfo(invocation, ct);
            if (symbolInfo.Symbol is not IMethodSymbol method) continue;

            var argIndex = argList.Arguments.IndexOf(arg);
            if (argIndex < 0 || argIndex >= method.Parameters.Length) continue;

            // Skip if argument is a literal or simple identifier matching the param name.
            var param = method.Parameters[argIndex];
            if (arg.Expression is LiteralExpressionSyntax ||
                arg.Expression is ObjectCreationExpressionSyntax)
            {
                var pos = text.Lines.GetLinePosition(arg.SpanStart);
                results.Add(new LspInlayHint
                {
                    Line   = pos.Line,
                    Column = pos.Character,
                    Label  = $"{param.Name}:",
                    Kind   = "parameter",
                });
            }
        }
    }

    // ── var type inference hints ──────────────────────────────────────────────

    /// <summary>
    /// Emits ": TypeName" hints after "var" keyword in local variable declarations
    /// where the type is not obvious from the right-hand side.
    /// </summary>
    private static void CollectVarTypeHints(
        SyntaxNode root, SemanticModel model, TextSpan span, SourceText text,
        List<LspInlayHint> results, CancellationToken ct)
    {
        foreach (var node in root.DescendantNodes(span))
        {
            if (ct.IsCancellationRequested) break;

            if (node is not VariableDeclarationSyntax varDecl) continue;
            if (varDecl.Type is not IdentifierNameSyntax { IsVar: true }) continue;

            foreach (var variable in varDecl.Variables)
            {
                var typeInfo = model.GetTypeInfo(varDecl.Type, ct);
                var type = typeInfo.Type;
                if (type is null || type.TypeKind == TypeKind.Error) continue;

                // Skip trivially obvious cases: literal rhs
                var initializer = variable.Initializer?.Value;
                if (initializer is LiteralExpressionSyntax) continue; // var x = 1; (int is obvious)
                if (initializer is ObjectCreationExpressionSyntax oce && oce.Type is not null) continue; // var x = new Foo()

                var typeDisplay = type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                if (string.IsNullOrEmpty(typeDisplay)) continue;

                // Place hint after the "var" keyword
                var varEnd = varDecl.Type.Span.End;
                var pos = text.Lines.GetLinePosition(varEnd);
                results.Add(new LspInlayHint
                {
                    Line   = pos.Line,
                    Column = pos.Character,
                    Label  = $": {typeDisplay}",
                    Kind   = "type",
                });
            }
        }
    }

    // ── Lambda return type hints ──────────────────────────────────────────────

    /// <summary>
    /// Emits "→ ReturnType" hints after the parameter list of lambda expressions
    /// where the return type is non-void and not explicitly stated.
    /// </summary>
    private static void CollectLambdaReturnTypeHints(
        SyntaxNode root, SemanticModel model, TextSpan span, SourceText text,
        List<LspInlayHint> results, CancellationToken ct)
    {
        foreach (var node in root.DescendantNodes(span))
        {
            if (ct.IsCancellationRequested) break;

            // Both ParenthesizedLambdaExpression and SimpleLambdaExpression
            SyntaxNode? arrowToken = null;
            ITypeSymbol? returnType = null;

            if (node is ParenthesizedLambdaExpressionSyntax parenLambda)
            {
                if (parenLambda.ReturnType is not null) continue; // already explicit
                arrowToken = parenLambda.ArrowToken.Parent;
                var sym = model.GetSymbolInfo(parenLambda, ct).Symbol as IMethodSymbol;
                returnType = sym?.ReturnType;
            }
            else if (node is SimpleLambdaExpressionSyntax simpleLambda)
            {
                arrowToken = simpleLambda.ArrowToken.Parent;
                var sym = model.GetSymbolInfo(simpleLambda, ct).Symbol as IMethodSymbol;
                returnType = sym?.ReturnType;
            }
            else continue;

            if (returnType is null || returnType.SpecialType == SpecialType.System_Void) continue;
            if (returnType.TypeKind == TypeKind.Error) continue;

            var typeDisplay = returnType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            if (string.IsNullOrEmpty(typeDisplay)) continue;

            // Place hint at the end of the "=>" arrow token's position
            var arrowPos = node is ParenthesizedLambdaExpressionSyntax pl
                ? pl.ArrowToken.Span.End
                : (node is SimpleLambdaExpressionSyntax sl ? sl.ArrowToken.Span.End : -1);
            if (arrowPos < 0) continue;

            var lp = text.Lines.GetLinePosition(arrowPos);
            results.Add(new LspInlayHint
            {
                Line   = lp.Line,
                Column = lp.Character,
                Label  = $" → {typeDisplay}",
                Kind   = "type",
            });
        }
    }

    private static void CollectVbHints(
        SyntaxNode root, SemanticModel model, TextSpan span, SourceText text,
        List<LspInlayHint> results, CancellationToken ct)
    {
        foreach (var node in root.DescendantNodes(span))
        {
            // VB.NET: SimpleArgumentSyntax is the equivalent of C# ArgumentSyntax.
            if (node is not Microsoft.CodeAnalysis.VisualBasic.Syntax.SimpleArgumentSyntax vbArg) continue;
            if (vbArg.NameColonEquals is not null) continue; // Already named.

            var argList = vbArg.Parent as Microsoft.CodeAnalysis.VisualBasic.Syntax.ArgumentListSyntax;
            if (argList is null) continue;

            var invocation = argList.Parent;
            if (invocation is null) continue;

            var symbolInfo = model.GetSymbolInfo(invocation, ct);
            if (symbolInfo.Symbol is not IMethodSymbol method) continue;

            var argIndex = argList.Arguments.IndexOf(vbArg);
            if (argIndex < 0 || argIndex >= method.Parameters.Length) continue;

            var param = method.Parameters[argIndex];
            var pos = text.Lines.GetLinePosition(vbArg.SpanStart);
            results.Add(new LspInlayHint
            {
                Line   = pos.Line,
                Column = pos.Character,
                Label  = $"{param.Name}:",
                Kind   = "parameter",
            });
        }
    }
}
