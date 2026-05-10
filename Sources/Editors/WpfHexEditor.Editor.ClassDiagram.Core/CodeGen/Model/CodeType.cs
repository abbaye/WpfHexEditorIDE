// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: CodeGen/Model/CodeType.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
// Created: 2026-05-08
// Description:
//     Immutable IR descriptor for a single type declaration: class,
//     interface, struct, enum, record, or delegate.
//
// Architecture Notes:
//     Carries everything a generator needs to emit the type without
//     re-consulting the diagram. Multiple CodeType instances can share
//     a CodeNamespace which itself sits inside CodeModel.
// ==========================================================

namespace WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Model;

/// <summary>Immutable IR descriptor for a type declaration.</summary>
public sealed record CodeType
{
    /// <summary>Type identifier name (without generic parameter list).</summary>
    public required string Name { get; init; }

    /// <summary>Structural kind — drives generator dispatch.</summary>
    public required CodeTypeKind Kind { get; init; }

    /// <summary>Accessibility level; defaults to <see cref="CodeAccessibility.Public"/>.</summary>
    public CodeAccessibility Accessibility { get; init; } = CodeAccessibility.Public;

    /// <summary>Fully qualified namespace; empty when declared at the global namespace.</summary>
    public string Namespace { get; init; } = string.Empty;

    /// <summary>Whether the type carries the <c>partial</c> modifier.</summary>
    public bool IsPartial { get; init; }

    /// <summary>Optional XML documentation summary (single line; generator expands into &lt;summary&gt;).</summary>
    public string? XmlDocSummary { get; init; }

    /// <summary>
    /// Base type reference (already in target language syntax) or <see langword="null"/>
    /// when there is no base type. Ignored for enums and interfaces.
    /// </summary>
    public string? BaseType { get; init; }

    /// <summary>
    /// Implemented or inherited interface names (already in target language syntax).
    /// </summary>
    public IReadOnlyList<string> ImplementedInterfaces { get; init; } = [];

    /// <summary>Generic type parameters; empty when the type is non-generic.</summary>
    public IReadOnlyList<CodeGenericParameter> GenericParameters { get; init; } = [];

    /// <summary>Attributes applied to the type itself.</summary>
    public IReadOnlyList<CodeAttribute> Attributes { get; init; } = [];

    /// <summary>Members declared inside the type.</summary>
    public IReadOnlyList<CodeMember> Members { get; init; } = [];

    /// <summary>
    /// For records: the primary-constructor positional parameters.
    /// Empty when the record has no positional members.
    /// </summary>
    public IReadOnlyList<CodeParameter> PrimaryConstructorParameters { get; init; } = [];

    /// <summary>
    /// For enums: an explicit underlying type (e.g. <c>byte</c>, <c>long</c>).
    /// <see langword="null"/> means default (<c>int</c> for C#).
    /// </summary>
    public string? EnumUnderlyingType { get; init; }
}
