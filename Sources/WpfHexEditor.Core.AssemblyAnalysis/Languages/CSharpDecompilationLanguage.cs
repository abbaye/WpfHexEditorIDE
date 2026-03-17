// ==========================================================
// Project: WpfHexEditor.Core.AssemblyAnalysis
// File: Languages/CSharpDecompilationLanguage.cs
// Author: Derek Tremblay
// Created: 2026-03-16
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     IDecompilationLanguage passthrough for C#.
//     Returns the ILSpy C# output unchanged and always reports success.
//     This is the identity transform — no allocation, no async work.
//
// Architecture Notes:
//     Pattern: Strategy (Null Object variant — identity transform).
//     Singleton via Instance property: the class is stateless and
//     can safely be shared across all decompilation calls.
// ==========================================================

namespace WpfHexEditor.Core.AssemblyAnalysis.Languages;

/// <summary>
/// C# decompilation language — passthrough, returns ILSpy output unchanged.
/// Always succeeds; never allocates during transform.
/// </summary>
public sealed class CSharpDecompilationLanguage : IDecompilationLanguage
{
    /// <summary>Singleton — the class is stateless; no benefit in multiple instances.</summary>
    public static readonly CSharpDecompilationLanguage Instance = new();

    private CSharpDecompilationLanguage() { }

    public string  Id                 => "CSharp";
    public string  DisplayName        => "C#";
    public string  FileExtension      => ".cs";
    public string? EditorLanguageName => "C#";

    /// <inheritdoc/>
    /// <remarks>Identity transform — returns the input unchanged, synchronously.</remarks>
    public Task<(string code, bool success)> TransformFromCSharpAsync(
        string csharpCode, CancellationToken ct)
        => Task.FromResult((csharpCode, true));
}
