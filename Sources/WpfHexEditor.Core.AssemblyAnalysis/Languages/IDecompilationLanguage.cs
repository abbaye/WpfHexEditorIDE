// ==========================================================
// Project: WpfHexEditor.Core.AssemblyAnalysis
// File: Languages/IDecompilationLanguage.cs
// Author: Derek Tremblay
// Created: 2026-03-16
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Strategy interface for post-decompilation language transforms.
//     The backend always produces C# (either skeleton or ILSpy full bodies).
//     Implementations either pass C# through unchanged (CSharpDecompilationLanguage)
//     or convert it to another .NET language (VbNetDecompilationLanguage, etc.).
//     On failure, implementations must return fallback content rather than throwing.
//
// Architecture Notes:
//     Pattern: Strategy — one implementation per target output language.
//     Core layer: BCL-only. Task<> is System.Threading.Tasks (BCL inbox in net8.0).
//     Registration: DecompilationLanguageRegistry.Register() at plugin startup.
//     Adding a new language: implement this interface + Register() — zero other changes.
// ==========================================================

namespace WpfHexEditor.Core.AssemblyAnalysis.Languages;

/// <summary>
/// Represents a target .NET output language for decompiled source code.
/// The backend always produces C#; implementations of this interface
/// either return it unchanged or convert it to another syntax.
/// </summary>
public interface IDecompilationLanguage
{
    /// <summary>
    /// Unique, stable identifier used in options persistence and cache keys.
    /// Must not change once shipped. Examples: "CSharp", "VBNet", "FSharp".
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Human-readable name shown in the Options ComboBox.
    /// Examples: "C#", "VB.NET", "F#".
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// File extension for extracted source files, including the leading dot.
    /// Examples: ".cs", ".vb", ".fs".
    /// </summary>
    string FileExtension { get; }

    /// <summary>
    /// Language identifier passed to <c>SetContentWithLinks</c> / <c>SetContentDirect</c>
    /// for syntax highlighting in the IDE code editor.
    /// Null when no syntax highlighting is available for this language.
    /// Examples: "C#", "VB.NET".
    /// </summary>
    string? EditorLanguageName { get; }

    /// <summary>
    /// Transforms ILSpy C# output into this language.
    /// The C# passthrough implementation returns the input unchanged.
    /// <para>
    /// Contract: must never throw except for <see cref="OperationCanceledException"/>.
    /// On any other failure, return the original C# with a diagnostic comment header.
    /// </para>
    /// </summary>
    /// <param name="csharpCode">C# source produced by ILSpy or the skeleton emitter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// (<paramref name="code"/> = transformed text or fallback C#,
    ///  <paramref name="success"/> = true when transform succeeded without errors).
    /// </returns>
    Task<(string code, bool success)> TransformFromCSharpAsync(
        string csharpCode, CancellationToken ct);
}
