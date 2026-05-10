// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: CodeGen/Languages/VisualBasic/VBGenericsBuilder.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
// Created: 2026-05-08
// Description:
//     Builds VB TypeParameterListSyntax with inline constraint
//     clauses ("Of T As {Class, New}") from CodeGenericParameter
//     IR descriptors.
// ==========================================================

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Model;

namespace WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Languages.VisualBasic;

internal static class VBGenericsBuilder
{
    public static TypeParameterListSyntax? BuildList(IReadOnlyList<CodeGenericParameter> parameters)
    {
        if (parameters.Count == 0)
            return null;

        var typeParameters = parameters.Select(BuildOne);
        return SyntaxFactory.TypeParameterList(SyntaxFactory.SeparatedList(typeParameters));
    }

    private static TypeParameterSyntax BuildOne(CodeGenericParameter p)
    {
        var node = SyntaxFactory.TypeParameter(SyntaxFactory.Identifier(p.Name));

        if (p.Constraints.Count == 0)
            return node;

        var constraints = p.Constraints.Select(BuildConstraint).ToList();
        var clause = constraints.Count == 1
            ? (TypeParameterConstraintClauseSyntax)SyntaxFactory.TypeParameterSingleConstraintClause(constraints[0])
            : SyntaxFactory.TypeParameterMultipleConstraintClause(SyntaxFactory.SeparatedList(constraints));

        return node.WithTypeParameterConstraintClause(clause);
    }

    private static ConstraintSyntax BuildConstraint(string raw)
    {
        var trimmed = raw.Trim();
        return trimmed switch
        {
            "class"   => SyntaxFactory.SpecialConstraint(SyntaxKind.ClassConstraint, SyntaxFactory.Token(SyntaxKind.ClassKeyword)),
            "struct"  => SyntaxFactory.SpecialConstraint(SyntaxKind.StructureConstraint, SyntaxFactory.Token(SyntaxKind.StructureKeyword)),
            "new()"   => SyntaxFactory.SpecialConstraint(SyntaxKind.NewConstraint, SyntaxFactory.Token(SyntaxKind.NewKeyword)),
            _         => SyntaxFactory.TypeConstraint(SyntaxFactory.ParseTypeName(trimmed))
        };
    }
}
