// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: CodeGen/Languages/CSharp/CSharpModifierBuilder.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
// Created: 2026-05-08
// Description:
//     Centralised computation of the SyntaxToken list (modifiers) that
//     Roslyn SyntaxFactory expects when building TypeDeclarationSyntax
//     and member declarations. Keeping this logic in one place avoids
//     scattered modifier handling across the type/member emitters.
// ==========================================================

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Model;

namespace WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Languages.CSharp;

internal static class CSharpModifierBuilder
{
    public static SyntaxTokenList ForType(CodeType type)
    {
        var tokens = new List<SyntaxToken>();
        foreach (var kind in CSharpAccessibilityHelper.ToTokens(type.Accessibility))
            tokens.Add(SyntaxFactory.Token(kind));

        switch (type.Kind)
        {
            case CodeTypeKind.AbstractClass:
                tokens.Add(SyntaxFactory.Token(SyntaxKind.AbstractKeyword));
                break;
            case CodeTypeKind.SealedClass:
                tokens.Add(SyntaxFactory.Token(SyntaxKind.SealedKeyword));
                break;
            case CodeTypeKind.StaticClass:
                tokens.Add(SyntaxFactory.Token(SyntaxKind.StaticKeyword));
                break;
            case CodeTypeKind.ReadOnlyStruct:
                tokens.Add(SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword));
                break;
        }

        if (type.IsPartial)
            tokens.Add(SyntaxFactory.Token(SyntaxKind.PartialKeyword));

        return SyntaxFactory.TokenList(tokens);
    }

    public static SyntaxTokenList ForMember(CodeMember member, CodeTypeKind owningTypeKind)
    {
        var tokens = new List<SyntaxToken>();

        // Interface members and enum members do not carry an explicit accessibility.
        if (owningTypeKind != CodeTypeKind.Interface && owningTypeKind != CodeTypeKind.Enum &&
            member.Accessibility != CodeAccessibility.NotApplicable)
        {
            foreach (var kind in CSharpAccessibilityHelper.ToTokens(member.Accessibility))
                tokens.Add(SyntaxFactory.Token(kind));
        }

        if (member.IsStatic)
            tokens.Add(SyntaxFactory.Token(SyntaxKind.StaticKeyword));

        if (member.IsAbstract && owningTypeKind != CodeTypeKind.Interface)
            tokens.Add(SyntaxFactory.Token(SyntaxKind.AbstractKeyword));
        else if (member.IsVirtual && !member.IsStatic && !member.IsOverride)
            tokens.Add(SyntaxFactory.Token(SyntaxKind.VirtualKeyword));

        if (member.IsOverride)
            tokens.Add(SyntaxFactory.Token(SyntaxKind.OverrideKeyword));

        if (member.IsSealed && member.IsOverride)
            tokens.Add(SyntaxFactory.Token(SyntaxKind.SealedKeyword));

        if (member.IsAsync)
            tokens.Add(SyntaxFactory.Token(SyntaxKind.AsyncKeyword));

        if (member.IsReadOnly)
            tokens.Add(SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword));

        return SyntaxFactory.TokenList(tokens);
    }
}
