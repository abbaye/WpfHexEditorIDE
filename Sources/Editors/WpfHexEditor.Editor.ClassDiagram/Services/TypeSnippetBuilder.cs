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
using WpfHexEditor.Editor.ClassDiagram.Core.RoundTrip.Abstractions;

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

    /// <summary>
    /// Phase A (ADR-037) — VB variant of <see cref="ForCSharp(ClassNode)"/>.
    /// Emits a minimal but valid VB type block that
    /// <see cref="VisualBasicRoundTripEditor.ApplyAddType"/> can consume.
    /// </summary>
    public static string ForVisualBasic(ClassNode node) => node.Kind switch
    {
        ClassKind.Interface => $"Public Interface {node.Name}\nEnd Interface",
        ClassKind.Struct    => $"Public Structure {node.Name}\nEnd Structure",
        // VB enums require ≥1 member to compile cleanly across all toolchains;
        // "None = 0" is the idiomatic placeholder (flag-zero pattern).
        ClassKind.Enum      => $"Public Enum {node.Name}\n    None = 0\nEnd Enum",
        _                   => node.IsAbstract
            ? $"Public MustInherit Class {node.Name}\nEnd Class"
            : $"Public Class {node.Name}\nEnd Class"
    };

    /// <summary>
    /// Returns the appropriate snippet for the language registered for
    /// <paramref name="node"/>'s source path. Resolution is delegated to
    /// <see cref="RoundTripEditorRegistry.TryGetByFilePath"/> so the
    /// snippet builder stays consistent with the round-trip routing.
    /// Falls back to C# when no path is set or no editor is registered.
    /// </summary>
    public static string ForLanguage(ClassNode node)
    {
        var editor = node.SourceFilePath is null
            ? null
            : RoundTripEditorRegistry.TryGetByFilePath(node.SourceFilePath);
        return editor?.LanguageId == LanguageIds.VisualBasic
            ? ForVisualBasic(node)
            : ForCSharp(node);
    }
}
