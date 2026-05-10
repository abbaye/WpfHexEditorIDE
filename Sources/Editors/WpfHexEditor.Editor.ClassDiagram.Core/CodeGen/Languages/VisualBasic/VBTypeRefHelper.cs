// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: CodeGen/Languages/VisualBasic/VBTypeRefHelper.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
// Created: 2026-05-08
// Description:
//     Helpers for building VB TypeSyntax instances from raw type
//     reference strings stored in the IR.
// ==========================================================

using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Options;

namespace WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Languages.VisualBasic;

internal static class VBTypeRefHelper
{
    public const string DefaultObjectType = "Object";
    public const string DefaultEventType = "EventHandler";

    /// <summary>Parses <paramref name="raw"/> as a VB type reference; falls back to <paramref name="fallback"/> when blank.</summary>
    public static TypeSyntax Parse(string? raw, string fallback = DefaultObjectType)
    {
        if (string.IsNullOrWhiteSpace(raw))
            raw = fallback;
        return SyntaxFactory.ParseTypeName(NormalizeGenerics(raw!));
    }

    /// <summary>Returns <c>Task</c> or <c>Task(Of T)</c> when the method is async, otherwise the parsed return type.</summary>
    public static TypeSyntax ForFunctionReturn(string? returnType, bool isAsync, CodeGenOptions options)
    {
        if (!isAsync || !options.EmitAsyncSignatures)
            return Parse(returnType);

        if (string.IsNullOrWhiteSpace(returnType) || returnType == "void")
            return SyntaxFactory.ParseTypeName("Task");

        return SyntaxFactory.ParseTypeName($"Task(Of {NormalizeGenerics(returnType)})");
    }

    /// <summary>Returns true when the IR return type indicates a Sub (no return value).</summary>
    public static bool IsVoid(string? returnType) =>
        string.IsNullOrWhiteSpace(returnType) || returnType == "void";

    private static string NormalizeGenerics(string raw) =>
        raw.Replace('<', '(').Replace('>', ')');
}
