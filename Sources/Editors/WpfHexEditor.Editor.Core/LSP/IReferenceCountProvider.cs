// ==========================================================
// Project: WpfHexEditor.Editor.Core
// File: LSP/IReferenceCountProvider.cs
// Contributors: Claude Sonnet 4.6
// Created: 2026-04-06
// Description:
//     Bridge interface allowing InlineHintsService (Editor.CodeEditor) to
//     request semantically-accurate reference counts without taking a hard
//     dependency on WpfHexEditor.Core.Roslyn.
//
// Architecture Notes:
//     Dual-path pattern: Roslyn path (via RoslynLanguageClient) when
//     CanProvide() == true; regex fallback otherwise.
// ==========================================================

namespace WpfHexEditor.Editor.Core.LSP;

/// <summary>
/// Provides semantically-accurate reference counts for a symbol at a given
/// declaration line. Implemented by <c>RoslynLanguageClient</c>; cast at
/// runtime via <c>ILspClient as IReferenceCountProvider</c>.
/// </summary>
public interface IReferenceCountProvider
{
    /// <summary>
    /// Returns <see langword="true"/> when Roslyn can provide a count for
    /// <paramref name="filePath"/> (i.e., the file is known to the workspace).
    /// Synchronous — no allocation.
    /// </summary>
    bool CanProvide(string filePath);

    /// <summary>
    /// Counts all references to the symbol named <paramref name="symbolName"/>
    /// declared at <paramref name="declarationLine"/> (0-based) in
    /// <paramref name="filePath"/>.
    /// Returns <see langword="null"/> on any Roslyn failure → caller falls back
    /// to regex counting.
    /// </summary>
    Task<int?> CountReferencesAsync(
        string filePath,
        int declarationLine,
        string symbolName,
        CancellationToken ct);
}
