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
//     Semantic properties (IsAsync, IsOverride, XmlDocSummary,
//     SourceFilePath, SourceLineOneBased, GenericConstraints) are
//     populated by RoslynClassDiagramAnalyzer; regex fallback leaves
//     them at defaults.
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

    /// <summary>Whether the member carries the <c>async</c> modifier (methods only).</summary>
    public bool IsAsync { get; init; }

    /// <summary>Whether the member carries the <c>override</c> modifier.</summary>
    public bool IsOverride { get; init; }

    /// <summary>
    /// Ordered list of parameter type-or-name tokens for method members.
    /// Empty for fields, properties, and events.
    /// Populated with full type names by the Roslyn path (e.g. "CancellationToken ct").
    /// </summary>
    public List<string> Parameters { get; init; } = [];

    /// <summary>
    /// Generic type parameter constraints for generic methods (e.g. "where T : IDisposable").
    /// <see langword="null"/> when not generic or not resolvable.
    /// </summary>
    public string? GenericConstraints { get; init; }

    /// <summary>
    /// Attribute names applied to this member (e.g. "Obsolete", "XmlElement").
    /// Populated by <c>RoslynClassDiagramAnalyzer</c>; empty when using the regex fallback.
    /// </summary>
    public List<string> Attributes { get; init; } = [];

    /// <summary>
    /// First-line XML documentation summary for this member.
    /// <see langword="null"/> when not present or when using the regex fallback.
    /// </summary>
    public string? XmlDocSummary { get; init; }

    /// <summary>
    /// Absolute path of the source file containing this member declaration.
    /// <see langword="null"/> when not resolvable.
    /// </summary>
    public string? SourceFilePath { get; init; }

    /// <summary>
    /// 1-based line number of this member in <see cref="SourceFilePath"/>.
    /// 0 when not resolvable.
    /// </summary>
    public int SourceLineOneBased { get; init; }

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
