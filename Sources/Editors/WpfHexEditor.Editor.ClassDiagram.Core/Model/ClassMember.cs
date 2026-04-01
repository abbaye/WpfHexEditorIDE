// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: Model/ClassMember.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-19
// Description:
//     Immutable record representing a single member (field, property,
//     method, or event) belonging to a class diagram node.
//
// Architecture Notes:
//     Implemented as a C# record to enable value-equality and
//     non-destructive mutation via `with` expressions.
//     DisplayLabel is a computed property — not stored in DSL.
//     Parameters list is for method signatures only; other kinds
//     leave it empty.
// ==========================================================

namespace WpfHexEditor.Editor.ClassDiagram.Core.Model;

/// <summary>
/// Represents a single member declared inside a class diagram node.
/// </summary>
public sealed record ClassMember
{
    /// <summary>The identifier name of the member.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// The declared return/field/property type name.
    /// Empty string when no type annotation is present in the DSL.
    /// </summary>
    public string TypeName { get; init; } = string.Empty;

    /// <summary>Structural kind of this member.</summary>
    public MemberKind Kind { get; init; } = MemberKind.Field;

    /// <summary>Access-modifier level.</summary>
    public MemberVisibility Visibility { get; init; } = MemberVisibility.Public;

    /// <summary>Whether the member carries the <c>static</c> modifier.</summary>
    public bool IsStatic { get; init; }

    /// <summary>Whether the member carries the <c>abstract</c> modifier.</summary>
    public bool IsAbstract { get; init; }

    /// <summary>
    /// Ordered list of parameter type-or-name tokens for method members.
    /// Empty for fields, properties, and events.
    /// </summary>
    public List<string> Parameters { get; init; } = [];

    // -------------------------------------------------------
    // Computed
    // -------------------------------------------------------

    /// <summary>
    /// A short human-readable label used by diagram renderers.
    /// Format: <c>Name</c> when no type is known, or <c>Name : TypeName</c> otherwise.
    /// </summary>
    public string DisplayLabel =>
        string.IsNullOrEmpty(TypeName)
            ? Name
            : $"{Name} : {TypeName}";
}
