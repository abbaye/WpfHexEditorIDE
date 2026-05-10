// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: CodeGen/Languages/CSharp/CSharpAccessibilityHelper.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
// Created: 2026-05-08
// Description:
//     Maps CodeAccessibility enum values to the matching Roslyn
//     SyntaxKind tokens used by SyntaxFactory.
// ==========================================================

using Microsoft.CodeAnalysis.CSharp;
using WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Model;

namespace WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Languages.CSharp;

internal static class CSharpAccessibilityHelper
{
    public static IReadOnlyList<SyntaxKind> ToTokens(CodeAccessibility accessibility) => accessibility switch
    {
        CodeAccessibility.Public             => [SyntaxKind.PublicKeyword],
        CodeAccessibility.Private            => [SyntaxKind.PrivateKeyword],
        CodeAccessibility.Protected          => [SyntaxKind.ProtectedKeyword],
        CodeAccessibility.Internal           => [SyntaxKind.InternalKeyword],
        CodeAccessibility.ProtectedInternal  => [SyntaxKind.ProtectedKeyword, SyntaxKind.InternalKeyword],
        CodeAccessibility.PrivateProtected   => [SyntaxKind.PrivateKeyword,   SyntaxKind.ProtectedKeyword],
        _                                    => []
    };
}
