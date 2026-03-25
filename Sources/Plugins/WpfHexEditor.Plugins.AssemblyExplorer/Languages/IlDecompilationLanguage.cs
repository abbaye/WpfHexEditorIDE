// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: Languages/IlDecompilationLanguage.cs
// Author: Derek Tremblay
// Created: 2026-03-23
// Description:
//     IL Disassembly pseudo-language for the decompilation submenu.
//     IL text is produced directly by the backend (GetIlText) — it is
//     never derived by transforming C#. TransformFromCSharpAsync must
//     not be called on this instance; it throws NotSupportedException.
//
// Architecture Notes:
//     Pattern: Strategy (IDecompilationLanguage).
//     Singleton — stateless; safe to share across all calls.
//     Registration: AssemblyExplorerPlugin.InitializeAsync →
//         DecompilationLanguageRegistry.Register(IlDecompilationLanguage.Instance)
//     The ViewModel's OpenSelectedNodeInEditorAsync detects Id == "IL" and
//     calls GetIlText directly instead of going through TransformFromCSharpAsync.
// ==========================================================

using System;
using System.Threading;
using System.Threading.Tasks;
using WpfHexEditor.Core.AssemblyAnalysis.Languages;

namespace WpfHexEditor.Plugins.AssemblyExplorer.Languages;

/// <summary>
/// IL Disassembly pseudo-language — produced directly by <c>IlTextEmitter</c>.
/// <para>
/// <see cref="TransformFromCSharpAsync"/> must never be called;
/// the ViewModel detects <c>Id == "IL"</c> and uses <c>GetIlText</c> instead.
/// </para>
/// </summary>
public sealed class IlDecompilationLanguage : IDecompilationLanguage
{
    /// <summary>Singleton — stateless; no benefit in multiple instances.</summary>
    public static readonly IlDecompilationLanguage Instance = new();

    private IlDecompilationLanguage() { }

    public string  Id                 => "IL";
    public string  DisplayName        => "IL Disassembly";
    public string  FileExtension      => ".il";
    public string? EditorLanguageName => null; // No syntax highlighting for IL yet
    public string  GlyphCode          => "\uE72E"; // Segoe MDL2 "List"

    /// <inheritdoc/>
    /// <exception cref="NotSupportedException">
    /// Always — IL text is produced directly by the backend, not transformed from C#.
    /// </exception>
    public Task<(string code, bool success)> TransformFromCSharpAsync(
        string csharpCode, CancellationToken ct)
        => throw new NotSupportedException(
            "IL Disassembly is produced directly by the backend (GetIlText). " +
            "Call DecompilerService.GetIlText instead of transforming C# source.");
}
