// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: RoundTrip/Abstractions/RoundTripEditorRegistry.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
// Created: 2026-05-10
// Description:
//     Process-wide registry mapping language identifiers and file
//     extensions to ILanguageRoundTripEditor implementations. Parallels
//     CodeGenLanguageRegistry (ADR-014). Will be re-exposed publicly via
//     Wht.SDK.Diagrams in Phase 7E so third-party plugins can register
//     editors for F#, Kotlin, Swift, etc.
//
// Architecture Notes:
//     Thread-safe via ConcurrentDictionary. Registration is idempotent on
//     LanguageId; the last registration wins, allowing plugins to override
//     built-in editors when explicitly desired.
// ==========================================================

using System.Collections.Concurrent;

namespace WpfHexEditor.Editor.ClassDiagram.Core.RoundTrip.Abstractions;

/// <summary>
/// Process-wide registry of <see cref="ILanguageRoundTripEditor"/>
/// implementations keyed by language id and file extension.
/// </summary>
public static class RoundTripEditorRegistry
{
    private static readonly ConcurrentDictionary<string, ILanguageRoundTripEditor> _byLanguageId =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly ConcurrentDictionary<string, ILanguageRoundTripEditor> _byExtension =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Registers <paramref name="editor"/> under its LanguageId and every declared extension.</summary>
    public static void Register(ILanguageRoundTripEditor editor)
    {
        ArgumentNullException.ThrowIfNull(editor);
        _byLanguageId[editor.LanguageId] = editor;
        foreach (var ext in editor.FileExtensions)
            _byExtension[ext] = editor;
    }

    /// <summary>Returns the editor for the given LanguageId, or null when none is registered.</summary>
    public static ILanguageRoundTripEditor? TryGetByLanguageId(string languageId) =>
        _byLanguageId.TryGetValue(languageId, out var ed) ? ed : null;

    /// <summary>Returns the editor whose declared extensions match the given file path, or null.</summary>
    public static ILanguageRoundTripEditor? TryGetByFilePath(string filePath)
    {
        string ext = System.IO.Path.GetExtension(filePath);
        return string.IsNullOrEmpty(ext)
            ? null
            : _byExtension.TryGetValue(ext, out var ed) ? ed : null;
    }

    /// <summary>Snapshot of all registered editors (stable order by LanguageId).</summary>
    public static IReadOnlyList<ILanguageRoundTripEditor> All() =>
        _byLanguageId.Values.OrderBy(e => e.LanguageId, StringComparer.Ordinal).ToArray();

    /// <summary>Removes every registration. Intended for tests only.</summary>
    public static void ResetForTests()
    {
        _byLanguageId.Clear();
        _byExtension.Clear();
    }
}
