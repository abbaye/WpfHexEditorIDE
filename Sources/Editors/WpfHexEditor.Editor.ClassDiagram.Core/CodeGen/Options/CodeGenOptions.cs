// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: CodeGen/Options/CodeGenOptions.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
// Created: 2026-05-08
// Description:
//     User-tunable knobs that drive the behaviour of every
//     ILanguageGenerator implementation. A single immutable record
//     so options can be passed safely across threads and stored
//     inside DiagramDocument for reproducible round-trip generation.
//
// Architecture Notes:
//     Record + with-expressions for non-destructive mutation.
//     New options must default to the most conservative value to
//     avoid breaking existing callers.
// ==========================================================

namespace WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Options;

/// <summary>Whitespace style for indentation.</summary>
public enum IndentStyle
{
    /// <summary>Indent with space characters.</summary>
    Spaces,

    /// <summary>Indent with tab characters.</summary>
    Tabs
}

/// <summary>C# language version that drives optional syntax features.</summary>
public enum CSharpLanguageVersion
{
    /// <summary>C# 7.3 — no nullable annotations, no records, no file-scoped namespaces.</summary>
    CSharp7_3,

    /// <summary>C# 8.0 — nullable annotations available.</summary>
    CSharp8,

    /// <summary>C# 9.0 — records, init-only setters.</summary>
    CSharp9,

    /// <summary>C# 10.0 — file-scoped namespaces, record struct.</summary>
    CSharp10,

    /// <summary>C# 11.0 — required members, generic attributes.</summary>
    CSharp11,

    /// <summary>C# 12.0 — primary constructors, collection expressions.</summary>
    CSharp12,

    /// <summary>Latest stable language version.</summary>
    Latest
}

/// <summary>
/// Immutable container for code-generation options shared by every language.
/// Language-specific generators read only the fields relevant to them and
/// ignore the rest, so a single instance can drive multi-language exports.
/// </summary>
public sealed record CodeGenOptions
{
    // -------------------------------------------------------
    // Common options
    // -------------------------------------------------------

    /// <summary>Root namespace emitted at the top of the generated file.</summary>
    public string RootNamespace { get; init; } = "GeneratedDiagram";

    /// <summary>Indentation style — spaces or tabs.</summary>
    public IndentStyle IndentStyle { get; init; } = IndentStyle.Spaces;

    /// <summary>Number of <see cref="IndentStyle"/> units per indentation level.</summary>
    public int IndentSize { get; init; } = 4;

    /// <summary>
    /// When <see langword="true"/>, emit <c>/// &lt;summary&gt;</c> XML doc blocks
    /// for types and members that carry an XmlDocSummary in the diagram model.
    /// </summary>
    public bool EmitXmlDocs { get; init; } = true;

    /// <summary>
    /// When <see langword="true"/>, emit <c>[Attribute]</c> decorations stored
    /// on diagram nodes and members.
    /// </summary>
    public bool EmitAttributes { get; init; } = true;

    /// <summary>
    /// When <see langword="true"/>, emit a top-of-file auto-generated banner.
    /// </summary>
    public bool EmitHeader { get; init; } = true;

    /// <summary>
    /// When <see langword="true"/>, generators should attempt to compile the
    /// final string before returning it (slower, but guarantees validity).
    /// </summary>
    public bool ValidateAfterGeneration { get; init; }

    // -------------------------------------------------------
    // C#-specific options
    // -------------------------------------------------------

    /// <summary>Target C# language version.</summary>
    public CSharpLanguageVersion CSharpVersion { get; init; } = CSharpLanguageVersion.Latest;

    /// <summary>
    /// When <see langword="true"/> and <see cref="CSharpVersion"/> is C# 10+,
    /// emit a file-scoped namespace declaration; otherwise emit a block-scoped one.
    /// </summary>
    public bool UseFileScopedNamespace { get; init; } = true;

    /// <summary>
    /// When <see langword="true"/>, emit <c>#nullable enable</c> at the top of the
    /// file and append <c>?</c> to nullable reference types.
    /// </summary>
    public bool NullableContextEnabled { get; init; } = true;

    /// <summary>
    /// When <see langword="true"/>, types flagged as records in the diagram model
    /// are emitted using <c>record</c> / <c>record struct</c> syntax (requires C# 9+).
    /// </summary>
    public bool PreferRecords { get; init; } = true;

    /// <summary>
    /// When <see langword="true"/>, partial classes are emitted as a single block
    /// rather than split across multiple files.
    /// </summary>
    public bool MergePartialDeclarations { get; init; } = true;

    /// <summary>
    /// When <see langword="true"/>, methods flagged as async include the
    /// <c>async</c> keyword and wrap the return type in <c>Task&lt;T&gt;</c>.
    /// </summary>
    public bool EmitAsyncSignatures { get; init; } = true;

    // -------------------------------------------------------
    // Factories
    // -------------------------------------------------------

    /// <summary>Default options matching the original skeleton generator behaviour.</summary>
    public static CodeGenOptions Default { get; } = new();

    /// <summary>Options aimed at C# 7.3 / .NET Framework consumers (no nullable, no records, no file-scoped namespaces).</summary>
    public static CodeGenOptions LegacyCSharp { get; } = new()
    {
        CSharpVersion = CSharpLanguageVersion.CSharp7_3,
        UseFileScopedNamespace = false,
        NullableContextEnabled = false,
        PreferRecords = false,
        EmitAsyncSignatures = false
    };

    /// <summary>Options aimed at strict, modern C# 12 with full nullable annotations.</summary>
    public static CodeGenOptions ModernCSharp { get; } = new()
    {
        CSharpVersion = CSharpLanguageVersion.CSharp12,
        UseFileScopedNamespace = true,
        NullableContextEnabled = true,
        PreferRecords = true,
        EmitAsyncSignatures = true
    };
}
