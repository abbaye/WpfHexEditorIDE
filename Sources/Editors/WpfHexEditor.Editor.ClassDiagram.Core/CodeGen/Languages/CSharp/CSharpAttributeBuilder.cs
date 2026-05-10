// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: CodeGen/Languages/CSharp/CSharpAttributeBuilder.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
// Created: 2026-05-08
// Description:
//     Builds Roslyn AttributeListSyntax nodes from CodeAttribute IR
//     descriptors.
// ==========================================================

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Model;

namespace WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Languages.CSharp;

internal static class CSharpAttributeBuilder
{
    public static SyntaxList<AttributeListSyntax> Build(IReadOnlyList<CodeAttribute> attributes)
    {
        if (attributes.Count == 0)
            return [];

        var lists = attributes
            .Select(BuildAttributeList)
            .ToList();

        return SyntaxFactory.List(lists);
    }

    private static AttributeListSyntax BuildAttributeList(CodeAttribute attribute)
    {
        var attr = SyntaxFactory.Attribute(SyntaxFactory.ParseName(attribute.Name));

        if (attribute.Arguments.Count > 0)
        {
            var args = attribute.Arguments
                .Select(a => SyntaxFactory.AttributeArgument(SyntaxFactory.ParseExpression(a)));
            attr = attr.WithArgumentList(
                SyntaxFactory.AttributeArgumentList(SyntaxFactory.SeparatedList(args)));
        }

        return SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(attr));
    }
}
