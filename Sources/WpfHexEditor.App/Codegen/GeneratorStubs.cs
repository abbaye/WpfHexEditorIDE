// Project      : WpfHexEditor.App
// File         : Codegen/GeneratorStubs.cs
// Description  : Compile-time stubs matching the exact signatures of FSharpGenerator,
//                RustGenerator and VBGenerator in whfmt.CodeGen.
//                These targets require the whfmt-codegen CLI tool; the stubs satisfy
//                ParserGenerator's switch expression without linking the actual files.

namespace WhfmtCodeGen.Generator;

internal static class FSharpGenerator
{
    public static string Generate(
        string formatName, string category, string version, string desc,
        string namespaceName, string className,
        List<ParserGenerator.BlockDef> blocks,
        List<ParserGenerator.ChecksumDef> checksums)
        => throw new NotSupportedException("F# generation requires the whfmt-codegen CLI tool.");
}

internal static class RustGenerator
{
    public static string Generate(
        string formatName, string category, string version, string desc,
        string className,
        List<ParserGenerator.BlockDef> blocks)
        => throw new NotSupportedException("Rust generation requires the whfmt-codegen CLI tool.");
}

internal static class VBGenerator
{
    public static string Generate(
        string formatName,
        string category,
        string version,
        string desc,
        string namespaceName,
        string className,
        List<ParserGenerator.BlockDef> blocks,
        List<ParserGenerator.ChecksumDef> checksums)
        => throw new NotSupportedException("VB.NET generation requires the whfmt-codegen CLI tool.");
}
