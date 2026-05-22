// Project      : WpfHexEditor.App
// File         : Codegen/WhfmtAppCodeGenerator.cs
// Description  : IDE-internal wrapper around ParserGenerator (linked source file).
//                Provides the C# / C# Span generation surface for the IDE without
//                referencing whfmt.CodeGen as a project dependency.
// Architecture : internal — never exposed in any NuGet package.
//                F# / Rust / VB targets are intentionally unsupported here;
//                the dialog disables those options and points to the CLI.

using WhfmtCodeGen.Generator;

namespace WpfHexEditor.App.Codegen;

internal static class WhfmtAppCodeGenerator
{
    /// <summary>
    /// Generates a C# or C# Span parser class from a .whfmt JSON definition.
    /// Throws <see cref="NotSupportedException"/> for F#, Rust and VB targets.
    /// Throws <see cref="Exception"/> (propagated from ParserGenerator) when the JSON is malformed.
    /// </summary>
    public static string Generate(
        string whfmtJson,
        string namespaceName,
        string className,
        OutputLanguage language  = OutputLanguage.CSharp,
        bool   validate          = true,
        bool   genAsync          = false)
    {
        if (language is OutputLanguage.FSharp or OutputLanguage.Rust or OutputLanguage.VisualBasic)
            throw new NotSupportedException(
                $"{language} generation is not available in the IDE. Use the whfmt-codegen CLI tool.");

        return ParserGenerator.GenerateFromJson(
            whfmtJson, namespaceName, className, validate, genAsync, language);
    }

    /// <summary>Returns true for languages that can be generated at IDE runtime.</summary>
    public static bool IsSupported(OutputLanguage language)
        => language is OutputLanguage.CSharp or OutputLanguage.CSharpSpan;
}
