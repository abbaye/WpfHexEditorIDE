// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: CodeGen/Abstractions/ILanguageGenerator.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
// Created: 2026-05-08
// Description:
//     Contract every language-specific code generator must implement
//     to participate in the CodeGen pipeline.
//
// Architecture Notes:
//     Pure functional contract: (CodeModel, CodeGenOptions) -> string.
//     No I/O, no static state. Generators must be safe to invoke from
//     any thread.
// ==========================================================

using WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Model;
using WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Options;

namespace WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Abstractions;

/// <summary>
/// Contract for a language-specific code generator that converts a
/// <see cref="CodeModel"/> into source text.
/// </summary>
public interface ILanguageGenerator
{
    /// <summary>Stable identifier of the target language (e.g. <c>csharp</c>, <c>vb</c>, <c>typescript</c>).</summary>
    string LanguageId { get; }

    /// <summary>Display name of the target language (e.g. "C#", "Visual Basic").</summary>
    string DisplayName { get; }

    /// <summary>Default file extension for generated files (e.g. <c>.cs</c>, <c>.vb</c>, <c>.ts</c>).</summary>
    string FileExtension { get; }

    /// <summary>Produces source text for <paramref name="model"/> honouring <paramref name="options"/>.</summary>
    string Generate(CodeModel model, CodeGenOptions options);
}
