// ==========================================================
// Project: whfmt.CodeGen
// File: Generator/OutputLanguage.cs
// Description: Output language options for the generate command.
// ==========================================================

namespace WhfmtCodeGen.Generator;

/// <summary>Target output language / mode for code generation.</summary>
public enum OutputLanguage
{
    /// <summary>Standard C# with BinaryReader (default).</summary>
    CSharp,
    /// <summary>Zero-alloc C# using ReadOnlySpan&lt;byte&gt; and MemoryMarshal.</summary>
    CSharpSpan,
    /// <summary>F# with discriminated unions and pattern matching.</summary>
    FSharp,
    /// <summary>Rust struct with impl From&lt;&amp;[u8]&gt;.</summary>
    Rust,
    /// <summary>VB.NET class with BinaryReader.</summary>
    VisualBasic,
}
