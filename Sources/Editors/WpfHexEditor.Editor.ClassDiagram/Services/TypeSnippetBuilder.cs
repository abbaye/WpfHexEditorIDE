// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram
// File: Services/TypeSnippetBuilder.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
// Created: 2026-05-11
// Description:
//     Phase 1B-6/7 — builds the minimal C# type-declaration snippet
//     used by AddType round-trip edits. Shared by ClassDiagramSplit-
//     Host.DuplicateNode/AddNewClass and DiagramCanvas.AddNodeAtMenuPoint
//     to avoid drift between two near-identical switches.
// ==========================================================

using WpfHexEditor.Editor.ClassDiagram.Core.Model;

namespace WpfHexEditor.Editor.ClassDiagram.Services;

internal static class TypeSnippetBuilder
{
    /// <summary>
    /// Returns a syntactically valid C# type declaration for the given node,
    /// suitable as the snippet argument of an <c>AddType</c> MemberEdit.
    /// </summary>
    public static string ForCSharp(ClassNode node) =>
        $"public {KindKeyword(node.Kind, node.IsAbstract)} {node.Name} {{ }}";

    /// <summary>
    /// Same as <see cref="ForCSharp(ClassNode)"/> but for a kind/abstract pair
    /// when no concrete <see cref="ClassNode"/> instance is in scope yet.
    /// </summary>
    public static string ForCSharp(string name, ClassKind kind, bool isAbstract = false) =>
        $"public {KindKeyword(kind, isAbstract)} {name} {{ }}";

    private static string KindKeyword(ClassKind kind, bool isAbstract) => kind switch
    {
        ClassKind.Interface => "interface",
        ClassKind.Struct    => "struct",
        ClassKind.Enum      => "enum",
        _                   => isAbstract ? "abstract class" : "class"
    };
}
