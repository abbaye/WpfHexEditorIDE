// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: CodeGen/Model/CodeMember.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
// Created: 2026-05-08
// Description:
//     Immutable IR descriptor for a member declared inside a CodeType.
//     Carries enough metadata for any ILanguageGenerator to emit a
//     correct member declaration in its target language.
//
// Architecture Notes:
//     Single record covers fields, properties, methods, events, and
//     indexers. Generators discriminate via Kind. Unused fields stay
//     at sensible defaults so the IR remains compact.
// ==========================================================

namespace WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Model;

/// <summary>Immutable IR descriptor for a single member of an IR type.</summary>
public sealed record CodeMember
{
    /// <summary>Member identifier name.</summary>
    public required string Name { get; init; }

    /// <summary>Kind of the member — drives generator dispatch.</summary>
    public required CodeMemberKind Kind { get; init; }

    /// <summary>
    /// Return type, field type, property type, or event handler type.
    /// Empty for constructors and enum members.
    /// </summary>
    public string ReturnType { get; init; } = string.Empty;

    /// <summary>Accessibility level.</summary>
    public CodeAccessibility Accessibility { get; init; } = CodeAccessibility.Public;

    /// <summary>Whether the member carries the <c>static</c> modifier.</summary>
    public bool IsStatic { get; init; }

    /// <summary>Whether the member carries the <c>abstract</c> modifier.</summary>
    public bool IsAbstract { get; init; }

    /// <summary>Whether the member carries the <c>virtual</c> modifier.</summary>
    public bool IsVirtual { get; init; }

    /// <summary>Whether the member carries the <c>override</c> modifier.</summary>
    public bool IsOverride { get; init; }

    /// <summary>Whether the member carries the <c>sealed</c> modifier.</summary>
    public bool IsSealed { get; init; }

    /// <summary>Whether the member carries the <c>async</c> modifier.</summary>
    public bool IsAsync { get; init; }

    /// <summary>Whether the member is read-only (field/property).</summary>
    public bool IsReadOnly { get; init; }

    /// <summary>Whether the property exposes a setter (when <see cref="Kind"/> is Property).</summary>
    public bool HasSetter { get; init; } = true;

    /// <summary>Whether the property setter is <c>init</c>-only (C# 9+).</summary>
    public bool IsInitOnly { get; init; }

    /// <summary>Whether the value type / reference type annotation marks it as nullable.</summary>
    public bool IsNullable { get; init; }

    /// <summary>Optional XML documentation summary (single line; generators expand into &lt;summary&gt;).</summary>
    public string? XmlDocSummary { get; init; }

    /// <summary>Optional initializer expression source text (e.g. <c>= 42</c>, <c>= new()</c>).</summary>
    public string? InitializerExpression { get; init; }

    /// <summary>Method/constructor parameters; empty for non-callable members.</summary>
    public IReadOnlyList<CodeParameter> Parameters { get; init; } = [];

    /// <summary>Generic type parameters declared on this member; empty for non-generic members.</summary>
    public IReadOnlyList<CodeGenericParameter> GenericParameters { get; init; } = [];

    /// <summary>Attributes applied to this member.</summary>
    public IReadOnlyList<CodeAttribute> Attributes { get; init; } = [];

    /// <summary>
    /// Optional explicit body source text. When <see langword="null"/> generators
    /// emit a default skeleton body (<c>throw new NotImplementedException();</c> or
    /// <c>return default!;</c>).
    /// </summary>
    public string? BodyText { get; init; }
}
