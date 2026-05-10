// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: CodeGen/Model/CodeAttribute.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
// Created: 2026-05-08
// Description:
//     Immutable IR descriptor for a single attribute decoration.
//
// Architecture Notes:
//     Arguments are stored as raw source-form strings so generators
//     can emit them verbatim without re-parsing. Type-aware emission
//     belongs to the ILanguageGenerator implementation, not the IR.
// ==========================================================

namespace WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Model;

/// <summary>Immutable IR descriptor for an attribute applied to a type or member.</summary>
public sealed record CodeAttribute
{
    /// <summary>Attribute type name without the optional <c>Attribute</c> suffix (e.g. <c>Obsolete</c>).</summary>
    public required string Name { get; init; }

    /// <summary>
    /// Positional and named arguments rendered verbatim (already valid in the target language).
    /// Empty collection means parameter-less attribute.
    /// </summary>
    public IReadOnlyList<string> Arguments { get; init; } = [];
}
