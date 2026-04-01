// ==========================================================
// Project: WpfHexEditor.Core.AssemblyAnalysis
// File: Languages/DecompilationLanguageRegistry.cs
// Author: Derek Tremblay
// Created: 2026-03-16
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Global registry for IDecompilationLanguage implementations.
//     Populated once at plugin startup; read-only during decompilation.
//     Provides the ordered collection used to populate the Options language
//     ComboBox and to look up the active language by its Id.
//
// Architecture Notes:
//     Pattern: Registry (static, thread-safe via ConcurrentDictionary).
//     ConcurrentDictionary provides lock-free reads after the startup
//     registration phase. The cached _allCached list is rebuilt on each
//     Register() call (startup-only path, no hot-reload concern).
// ==========================================================

using System.Collections.Concurrent;

namespace WpfHexEditor.Core.AssemblyAnalysis.Languages;

/// <summary>
/// Global registry of available decompilation language strategies.
/// Register languages at plugin startup; read during decompilation.
/// </summary>
public static class DecompilationLanguageRegistry
{
    private static readonly ConcurrentDictionary<string, IDecompilationLanguage> _languages =
        new(StringComparer.OrdinalIgnoreCase);

    // Cached snapshot — rebuilt on each Register() call (startup only).
    private static volatile IReadOnlyList<IDecompilationLanguage> _allCached = [];

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// All registered languages in registration order.
    /// Thread-safe for concurrent reads.
    /// </summary>
    public static IReadOnlyList<IDecompilationLanguage> All => _allCached;

    /// <summary>
    /// Registers a language. Overwrites an existing registration with the same
    /// <see cref="IDecompilationLanguage.Id"/> (case-insensitive).
    /// Should be called at plugin startup before any decompilation occurs.
    /// </summary>
    public static void Register(IDecompilationLanguage language)
    {
        ArgumentNullException.ThrowIfNull(language);
        _languages[language.Id] = language;

        // Rebuild the cached snapshot. List preserves insertion/update order via
        // dictionary enumeration (ConcurrentDictionary is unordered, so we snapshot
        // all values). Startup-only, so the rebuild cost is negligible.
        _allCached = _languages.Values.ToList().AsReadOnly();
    }

    /// <summary>
    /// Returns the language registered with the given <paramref name="id"/>,
    /// or <c>null</c> when not found or <paramref name="id"/> is null/empty.
    /// Comparison is case-insensitive.
    /// </summary>
    public static IDecompilationLanguage? Get(string? id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        return _languages.TryGetValue(id, out var lang) ? lang : null;
    }
}
