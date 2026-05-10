// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: CodeGen/Model/CodeModel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
// Created: 2026-05-08
// Description:
//     Root aggregate of the language-agnostic IR consumed by every
//     ILanguageGenerator. Built by DiagramToCodeModelMapper from a
//     DiagramDocument and a CodeGenOptions instance.
//
// Architecture Notes:
//     Immutable record. Generators must treat the model as read-only
//     and produce strings without mutating any input.
// ==========================================================

namespace WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Model;

/// <summary>
/// Immutable IR root containing every type that should be emitted plus the
/// using/import directives that go at the top of the generated file.
/// </summary>
public sealed record CodeModel
{
    /// <summary>Default namespace for types whose <see cref="CodeType.Namespace"/> is empty.</summary>
    public string RootNamespace { get; init; } = "GeneratedDiagram";

    /// <summary>Using/import directives emitted before the type declarations.</summary>
    public IReadOnlyList<CodeUsing> Usings { get; init; } = [];

    /// <summary>Types contained in this model, in emission order.</summary>
    public IReadOnlyList<CodeType> Types { get; init; } = [];
}
