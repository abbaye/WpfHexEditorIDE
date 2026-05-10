// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: CodeGen/Languages/CSharp/CSharpGenericsBuilder.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
// Created: 2026-05-08
// Description:
//     Builds TypeParameterListSyntax and TypeParameterConstraintClauseSyntax
//     instances from CodeGenericParameter IR descriptors.
// ==========================================================

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Model;

namespace WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Languages.CSharp;

internal static class CSharpGenericsBuilder
{
    public static TypeParameterListSyntax? BuildList(IReadOnlyList<CodeGenericParameter> parameters)
    {
        if (parameters.Count == 0)
            return null;

        var typeParameters = parameters
            .Select(p => SyntaxFactory.TypeParameter(SyntaxFactory.Identifier(p.Name)));

        return SyntaxFactory.TypeParameterList(SyntaxFactory.SeparatedList(typeParameters));
    }

    public static SyntaxList<TypeParameterConstraintClauseSyntax> BuildConstraints(
        IReadOnlyList<CodeGenericParameter> parameters)
    {
        var clauses = parameters
            .Where(p => p.Constraints.Count > 0)
            .Select(BuildClause)
            .ToList();

        return clauses.Count == 0 ? [] : SyntaxFactory.List(clauses);
    }

    private static TypeParameterConstraintClauseSyntax BuildClause(CodeGenericParameter p)
    {
        var constraints = p.Constraints.Select(BuildConstraint);
        return SyntaxFactory.TypeParameterConstraintClause(
            SyntaxFactory.IdentifierName(p.Name),
            SyntaxFactory.SeparatedList(constraints));
    }

    private static TypeParameterConstraintSyntax BuildConstraint(string raw)
    {
        var trimmed = raw.Trim();
        return trimmed switch
        {
            "class"   => SyntaxFactory.ClassOrStructConstraint(SyntaxKind.ClassConstraint),
            "struct"  => SyntaxFactory.ClassOrStructConstraint(SyntaxKind.StructConstraint),
            "new()"   => SyntaxFactory.ConstructorConstraint(),
            "notnull" => SyntaxFactory.TypeConstraint(SyntaxFactory.ParseTypeName("notnull")),
            _         => SyntaxFactory.TypeConstraint(SyntaxFactory.ParseTypeName(trimmed))
        };
    }
}
