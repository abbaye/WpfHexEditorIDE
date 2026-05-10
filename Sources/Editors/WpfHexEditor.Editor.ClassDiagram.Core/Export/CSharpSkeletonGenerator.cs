// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: Export/CSharpSkeletonGenerator.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6, Claude Opus 4.7
// Created: 2026-03-19
// Description:
//     Backward-compatible thin façade that delegates C# skeleton
//     generation to the CodeGenerationPipeline (Roslyn SyntaxFactory).
//     The original public surface is preserved so existing callers
//     (e.g. ClassDiagramExportService) continue to work unchanged.
//
// Architecture Notes:
//     The body of this method now produces syntactically validated C#
//     thanks to Roslyn. Output formatting differs slightly from the
//     legacy StringBuilder version — callers that depend on the exact
//     legacy format should pin CodeGenOptions accordingly or migrate
//     to CodeGenerationPipeline.GenerateCSharp directly.
// ==========================================================

using WpfHexEditor.Editor.ClassDiagram.Core.CodeGen;
using WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Options;
using WpfHexEditor.Editor.ClassDiagram.Core.Model;

namespace WpfHexEditor.Editor.ClassDiagram.Core.Export;

/// <summary>
/// Backward-compatible façade that produces a valid C# skeleton source
/// from a <see cref="DiagramDocument"/> by delegating to the new
/// <see cref="CodeGenerationPipeline"/>.
/// </summary>
public static class CSharpSkeletonGenerator
{
    /// <summary>
    /// Generates C# skeleton source text for all types in <paramref name="doc"/>
    /// using the default code-generation options.
    /// </summary>
    public static string Generate(DiagramDocument doc) =>
        CodeGenerationPipeline.GenerateCSharp(doc, CodeGenOptions.Default);

    /// <summary>
    /// Generates C# skeleton source text for all types in <paramref name="doc"/>
    /// honouring the supplied <paramref name="options"/>.
    /// </summary>
    public static string Generate(DiagramDocument doc, CodeGenOptions options) =>
        CodeGenerationPipeline.GenerateCSharp(doc, options);
}
