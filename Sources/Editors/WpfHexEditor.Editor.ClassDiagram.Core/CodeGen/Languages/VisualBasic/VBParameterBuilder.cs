// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: CodeGen/Languages/VisualBasic/VBParameterBuilder.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
// Created: 2026-05-08
// Description:
//     Builds Roslyn VB ParameterListSyntax instances from
//     CodeParameter IR descriptors. VB uses ByVal/ByRef/ParamArray
//     and Optional, with default values introduced by `=`.
// ==========================================================

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Model;

namespace WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Languages.VisualBasic;

internal static class VBParameterBuilder
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
        var modifiers = ResolveModifierTokens(p);
        var asClause = SyntaxFactory.SimpleAsClause(VBTypeRefHelper.Parse(p.Type));

        var parameter = SyntaxFactory.Parameter(SyntaxFactory.ModifiedIdentifier(p.Name))
            .WithAsClause(asClause);

        if (modifiers.Count > 0)
            parameter = parameter.WithModifiers(SyntaxFactory.TokenList(modifiers));

        if (!string.IsNullOrWhiteSpace(p.DefaultValue))
        {
            parameter = parameter.WithDefault(
                SyntaxFactory.EqualsValue(SyntaxFactory.ParseExpression(p.DefaultValue)));
        }

        return parameter;
    }

    private static List<SyntaxToken> ResolveModifierTokens(CodeParameter p)
    {
        var tokens = new List<SyntaxToken>();
        if (!string.IsNullOrWhiteSpace(p.DefaultValue))
            tokens.Add(SyntaxFactory.Token(SyntaxKind.OptionalKeyword));

        switch (p.Modifier)
        {
            case CodeParameterModifier.Ref:
            case CodeParameterModifier.Out:
            case CodeParameterModifier.In:
                tokens.Add(SyntaxFactory.Token(SyntaxKind.ByRefKeyword));
                break;
            case CodeParameterModifier.Params:
                tokens.Add(SyntaxFactory.Token(SyntaxKind.ParamArrayKeyword));
                break;
            default:
                tokens.Add(SyntaxFactory.Token(SyntaxKind.ByValKeyword));
                break;
        }

        return tokens;
    }
}
