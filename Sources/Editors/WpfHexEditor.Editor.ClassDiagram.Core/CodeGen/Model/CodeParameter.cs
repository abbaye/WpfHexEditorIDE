// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: CodeGen/Model/CodeParameter.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
// Created: 2026-05-08
// Description:
//     Immutable IR descriptor for a single method parameter.
// ==========================================================

namespace WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Model;

/// <summary>By-ref modifier of a method parameter.</summary>
public enum CodeParameterModifier
{
    /// <summary>Pass-by-value (default).</summary>
    None,

    /// <summary>Pass-by-reference (<c>ref</c>).</summary>
    Ref,

    /// <summary>Pass-by-reference, write-only (<c>out</c>).</summary>
    Out,

    /// <summary>Pass-by-reference, read-only (<c>in</c>).</summary>
    In,

    /// <summary>Variable-arity (<c>params</c>).</summary>
    Params,

    /// <summary>Extension-method receiver (<c>this</c>).</summary>
    This
}

/// <summary>Immutable IR descriptor for a single method parameter.</summary>
public sealed record CodeParameter
{
    /// <summary>Parameter identifier name.</summary>
    public required string Name { get; init; }

    /// <summary>Declared parameter type (already in the target language's syntax).</summary>
    public required string Type { get; init; }

    /// <summary>By-ref modifier; defaults to <see cref="CodeParameterModifier.None"/>.</summary>
    public CodeParameterModifier Modifier { get; init; } = CodeParameterModifier.None;

    /// <summary>Optional default value source text (e.g. <c>null</c>, <c>0</c>, <c>"foo"</c>).</summary>
    public string? DefaultValue { get; init; }

    /// <summary>Attributes applied to this parameter.</summary>
    public IReadOnlyList<CodeAttribute> Attributes { get; init; } = [];
}
