// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: CodeGen/Languages/CSharp/CSharpTypeRefHelper.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
// Created: 2026-05-08
// Description:
//     Helpers for building TypeSyntax instances from raw C# type
//     reference strings stored in the IR.
// ==========================================================

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Options;

namespace WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Languages.CSharp;

internal static class CSharpTypeRefHelper
{
    public const string DefaultObjectType = "object";
    public const string DefaultEventType = "EventHandler";

    /// <summary>
    /// Parses <paramref name="raw"/> as a C# type reference. Returns the void type
    /// when <paramref name="raw"/> is null/empty and <paramref name="allowVoid"/> is true,
    /// or <see cref="DefaultObjectType"/> otherwise.
    /// </summary>
    public static TypeSyntax Parse(string? raw, bool allowVoid = false, string fallback = DefaultObjectType)
    {
        if (string.IsNullOrWhiteSpace(raw))
            raw = allowVoid ? "void" : fallback;
        return SyntaxFactory.ParseTypeName(raw);
    }

    /// <summary>
    /// Wraps <paramref name="returnType"/> in <c>Task&lt;T&gt;</c> (or <c>Task</c>) when
    /// <paramref name="isAsync"/> is true and <paramref name="options"/> allows it.
    /// </summary>
    public static TypeSyntax ForMethodReturn(string? returnType, bool isAsync, CodeGenOptions options)
    {
        if (!isAsync || !options.EmitAsyncSignatures)
            return Parse(returnType, allowVoid: true);

        if (string.IsNullOrWhiteSpace(returnType) || returnType == "void")
            return SyntaxFactory.ParseTypeName("Task");

        return SyntaxFactory.ParseTypeName($"Task<{returnType}>");
    }
}
