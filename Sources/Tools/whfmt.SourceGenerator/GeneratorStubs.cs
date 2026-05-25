// Compile-time stubs for non-C# generators — these are referenced by ParserGenerator.cs
// but will never execute from the Source Generator (only CSharp/CSharpSpan are routed).
// The actual implementations live in whfmt.CodeGen (CLI tool).

using System.Collections.Generic;

namespace WhfmtCodeGen.Generator;

internal static class FSharpGenerator
{
    internal static string Generate(
        string formatName, string category, string version, string desc,
        string namespaceName, string className,
        List<ParserGenerator.BlockDef> blocks,
        List<ParserGenerator.ChecksumDef> checksums)
        => throw new System.NotSupportedException(
            "F# generation is not supported in the Roslyn Source Generator. Use the whfmt-codegen CLI tool instead.");
}

internal static class RustGenerator
{
    internal static string Generate(
        string formatName, string category, string version, string desc,
        string className,
        List<ParserGenerator.BlockDef> blocks)
        => throw new System.NotSupportedException(
            "Rust generation is not supported in the Roslyn Source Generator. Use the whfmt-codegen CLI tool instead.");
}

internal static class VBGenerator
{
    internal static string Generate(
        string formatName, string category, string version, string desc,
        string namespaceName, string className,
        List<ParserGenerator.BlockDef> blocks,
        List<ParserGenerator.ChecksumDef> checksums)
        => throw new System.NotSupportedException(
            "VB.NET generation is not supported in the Roslyn Source Generator. Use the whfmt-codegen CLI tool instead.");
}
