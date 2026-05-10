// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: CodeGen/Languages/VisualBasic/VBAttributeBuilder.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
// Created: 2026-05-08
// Description:
//     Builds Roslyn AttributeListSyntax nodes for VisualBasic from
//     CodeAttribute IR descriptors.
// ==========================================================

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Model;

namespace WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Languages.VisualBasic;

internal static class VBAttributeBuilder
{
    public static SyntaxList<AttributeListSyntax> Build(IReadOnlyList<CodeAttribute> attributes)
    {
        if (attributes.Count == 0)
            return [];

        return SyntaxFactory.List(attributes.Select(BuildAttributeList));
    }

    private static AttributeListSyntax BuildAttributeList(CodeAttribute attribute)
    {
        var attr = SyntaxFactory.Attribute(SyntaxFactory.ParseName(attribute.Name));

        if (attribute.Arguments.Count > 0)
        {
            var args = attribute.Arguments
                .Select(a => SyntaxFactory.SimpleArgument(SyntaxFactory.ParseExpression(a)));
            attr = attr.WithArgumentList(
                SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList<ArgumentSyntax>(args)));
        }

        return SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(attr));
    }
}
