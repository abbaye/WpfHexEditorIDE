// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: CodeGen/Languages/VisualBasic/VBModifierBuilder.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
// Created: 2026-05-08
// Description:
//     Computes the SyntaxToken list expected by VisualBasic Roslyn
//     factories for type and member declarations. VB uses different
//     keywords than C# (MustInherit/NotInheritable/MustOverride/Shared).
// ==========================================================

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.VisualBasic;
using WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Model;

namespace WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Languages.VisualBasic;

internal static class VBModifierBuilder
{
    public static SyntaxTokenList ForType(CodeType type)
    {
        var tokens = new List<SyntaxToken>();
        foreach (var kind in VBAccessibilityHelper.ToTokens(type.Accessibility))
            tokens.Add(SyntaxFactory.Token(kind));

        switch (type.Kind)
        {
            case CodeTypeKind.AbstractClass:
                tokens.Add(SyntaxFactory.Token(SyntaxKind.MustInheritKeyword));
                break;
            case CodeTypeKind.SealedClass:
                tokens.Add(SyntaxFactory.Token(SyntaxKind.NotInheritableKeyword));
                break;
        }

        if (type.IsPartial)
            tokens.Add(SyntaxFactory.Token(SyntaxKind.PartialKeyword));

        return SyntaxFactory.TokenList(tokens);
    }

    public static SyntaxTokenList ForMember(CodeMember member, CodeTypeKind owningTypeKind)
    {
        var tokens = new List<SyntaxToken>();

        if (owningTypeKind != CodeTypeKind.Interface && owningTypeKind != CodeTypeKind.Enum &&
            member.Accessibility != CodeAccessibility.NotApplicable)
        {
            foreach (var kind in VBAccessibilityHelper.ToTokens(member.Accessibility))
                tokens.Add(SyntaxFactory.Token(kind));
        }

        if (member.IsStatic)
            tokens.Add(SyntaxFactory.Token(SyntaxKind.SharedKeyword));

        if (member.IsAbstract && owningTypeKind != CodeTypeKind.Interface)
            tokens.Add(SyntaxFactory.Token(SyntaxKind.MustOverrideKeyword));
        else if (member.IsVirtual && !member.IsStatic && !member.IsOverride)
            tokens.Add(SyntaxFactory.Token(SyntaxKind.OverridableKeyword));

        if (member.IsOverride)
            tokens.Add(SyntaxFactory.Token(SyntaxKind.OverridesKeyword));

        if (member.IsAsync)
            tokens.Add(SyntaxFactory.Token(SyntaxKind.AsyncKeyword));

        if (member.IsReadOnly)
            tokens.Add(SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword));

        return SyntaxFactory.TokenList(tokens);
    }
}
