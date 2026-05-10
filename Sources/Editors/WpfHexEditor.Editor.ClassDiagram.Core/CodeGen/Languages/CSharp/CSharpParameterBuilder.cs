// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: CodeGen/Languages/CSharp/CSharpParameterBuilder.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
// Created: 2026-05-08
// Description:
//     Builds Roslyn ParameterListSyntax instances from CodeParameter
//     IR descriptors.
// ==========================================================

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Model;

namespace WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Languages.CSharp;

internal static class CSharpParameterBuilder
{
    public static ParameterListSyntax Build(IReadOnlyList<CodeParameter> parameters)
    {
        if (parameters.Count == 0)
            return SyntaxFactory.ParameterList();

        var nodes = parameters.Select(BuildOne);
        return SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(nodes));
    }

    private static ParameterSyntax BuildOne(CodeParameter p)
    {
        var parameter = SyntaxFactory.Parameter(SyntaxFactory.Identifier(p.Name))
            .WithType(SyntaxFactory.ParseTypeName(p.Type));

        var modifiers = ResolveModifierTokens(p.Modifier);
        if (modifiers.Count > 0)
            parameter = parameter.WithModifiers(SyntaxFactory.TokenList(modifiers));

        if (!string.IsNullOrWhiteSpace(p.DefaultValue))
        {
            parameter = parameter.WithDefault(
                SyntaxFactory.EqualsValueClause(SyntaxFactory.ParseExpression(p.DefaultValue)));
        }

        return parameter;
    }

    private static List<SyntaxToken> ResolveModifierTokens(CodeParameterModifier modifier) => modifier switch
    {
        CodeParameterModifier.Ref    => [SyntaxFactory.Token(SyntaxKind.RefKeyword)],
        CodeParameterModifier.Out    => [SyntaxFactory.Token(SyntaxKind.OutKeyword)],
        CodeParameterModifier.In     => [SyntaxFactory.Token(SyntaxKind.InKeyword)],
        CodeParameterModifier.Params => [SyntaxFactory.Token(SyntaxKind.ParamsKeyword)],
        CodeParameterModifier.This   => [SyntaxFactory.Token(SyntaxKind.ThisKeyword)],
        _                            => []
    };
}
