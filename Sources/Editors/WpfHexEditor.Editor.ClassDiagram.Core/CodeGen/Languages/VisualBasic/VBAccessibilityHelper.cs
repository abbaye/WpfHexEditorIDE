// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: CodeGen/Languages/VisualBasic/VBAccessibilityHelper.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
// Created: 2026-05-08
// Description:
//     Maps CodeAccessibility enum values to the matching Roslyn
//     VisualBasic SyntaxKind tokens. VB uses Friend instead of
//     internal, ProtectedFriend instead of protected internal,
//     and PrivateProtected instead of private protected.
// ==========================================================

using Microsoft.CodeAnalysis.VisualBasic;
using WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Model;

namespace WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Languages.VisualBasic;

internal static class VBAccessibilityHelper
{
    public static IReadOnlyList<SyntaxKind> ToTokens(CodeAccessibility accessibility) => accessibility switch
    {
        CodeAccessibility.Public             => [SyntaxKind.PublicKeyword],
        CodeAccessibility.Private            => [SyntaxKind.PrivateKeyword],
        CodeAccessibility.Protected          => [SyntaxKind.ProtectedKeyword],
        CodeAccessibility.Internal           => [SyntaxKind.FriendKeyword],
        CodeAccessibility.ProtectedInternal  => [SyntaxKind.ProtectedKeyword, SyntaxKind.FriendKeyword],
        CodeAccessibility.PrivateProtected   => [SyntaxKind.PrivateKeyword,   SyntaxKind.ProtectedKeyword],
        _                                    => []
    };
}
