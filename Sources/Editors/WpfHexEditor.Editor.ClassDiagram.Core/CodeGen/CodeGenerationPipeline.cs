// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: CodeGen/CodeGenerationPipeline.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
// Created: 2026-05-08
// Description:
//     Top-level façade that wires DiagramToCodeModelMapper to a
//     concrete ILanguageGenerator. Single entry-point for callers
//     who want "DiagramDocument -> source string" without managing
//     the IR by hand.
// ==========================================================

using WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Abstractions;
using WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Languages.CSharp;
using WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Languages.VisualBasic;
using WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Mapping;
using WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Options;
using WpfHexEditor.Editor.ClassDiagram.Core.Model;

namespace WpfHexEditor.Editor.ClassDiagram.Core.CodeGen;

/// <summary>
/// Façade that turns a <see cref="DiagramDocument"/> into a source string by
/// chaining the IR mapper and a registered <see cref="ILanguageGenerator"/>.
/// </summary>
public static class CodeGenerationPipeline
{
    static CodeGenerationPipeline()
    {
        CodeGenLanguageRegistry.Register(new CSharpGenerator());
        CodeGenLanguageRegistry.Register(new VBGenerator());
    }

    /// <summary>
    /// Generates source for <paramref name="document"/> in the language identified
    /// by <paramref name="languageId"/>. Throws when no matching generator is registered.
    /// </summary>
    public static string Generate(DiagramDocument document, string languageId, CodeGenOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrEmpty(languageId);

        var generator = CodeGenLanguageRegistry.Resolve(languageId)
            ?? throw new InvalidOperationException(
                $"No code generator registered for language id '{languageId}'.");

        var resolvedOptions = options ?? CodeGenOptions.Default;
        var model = DiagramToCodeModelMapper.Map(document, resolvedOptions);
        return generator.Generate(model, resolvedOptions);
    }

    /// <summary>
    /// Convenience overload that picks the C# generator (<see cref="LanguageIds.CSharp"/>).
    /// </summary>
    public static string GenerateCSharp(DiagramDocument document, CodeGenOptions? options = null) =>
        Generate(document, LanguageIds.CSharp, options);
}
