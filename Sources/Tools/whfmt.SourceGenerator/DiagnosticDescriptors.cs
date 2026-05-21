// Project      : whfmt.SourceGenerator
// File         : DiagnosticDescriptors.cs
// Description  : Roslyn diagnostic IDs emitted by WhfmtIncrementalGenerator.

using Microsoft.CodeAnalysis;

namespace WhfmtSourceGenerator;

internal static class DiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor GenerationFailed = new(
        id:                 "WHSG001",
        title:              "whfmt source generation failed",
        messageFormat:      "Failed to generate parser from '{0}': {1}",
        category:           "whfmt.SourceGenerator",
        defaultSeverity:    DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description:        "The .whfmt file could not be parsed or contains an unsupported schema construct. " +
                            "Check the file follows whfmt v3 schema and that all block types are supported.");

    public static readonly DiagnosticDescriptor UnsupportedLanguage = new(
        id:                 "WHSG002",
        title:              "whfmt language target not supported by Source Generator",
        messageFormat:      "Language '{0}' is not supported by whfmt.SourceGenerator. " +
                            "Use the whfmt-codegen CLI tool instead. Falling back to CSharp.",
        category:           "whfmt.SourceGenerator",
        defaultSeverity:    DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description:        "The whfmt.SourceGenerator only supports CSharp and CSharpSpan output. " +
                            "For FSharp, Rust or VisualBasic targets, use: whfmt-codegen generate --lang <lang>");
}
