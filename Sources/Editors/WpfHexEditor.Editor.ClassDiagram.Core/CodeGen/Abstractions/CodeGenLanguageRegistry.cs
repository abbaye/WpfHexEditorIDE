// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: CodeGen/Abstractions/CodeGenLanguageRegistry.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
// Created: 2026-05-08
// Description:
//     Thread-safe registry of ILanguageGenerator instances keyed by
//     their LanguageId. Allows callers to discover and invoke
//     generators without compile-time references.
//
// Architecture Notes:
//     Static facade with built-in defaults (C#) registered eagerly.
//     Plugins may register additional generators (VB, TS, …) by
//     calling Register on application startup.
// ==========================================================

using System.Collections.Concurrent;
using WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Abstractions;

namespace WpfHexEditor.Editor.ClassDiagram.Core.CodeGen;

/// <summary>Thread-safe registry mapping language ids to generator instances.</summary>
public static class CodeGenLanguageRegistry
{
    private static readonly ConcurrentDictionary<string, ILanguageGenerator> Generators =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers <paramref name="generator"/> under its <see cref="ILanguageGenerator.LanguageId"/>.
    /// Existing entries with the same id are replaced.
    /// </summary>
    public static void Register(ILanguageGenerator generator)
    {
        ArgumentNullException.ThrowIfNull(generator);
        Generators[generator.LanguageId] = generator;
    }

    /// <summary>Removes the generator registered under <paramref name="languageId"/>, if any.</summary>
    public static bool Unregister(string languageId) =>
        Generators.TryRemove(languageId, out _);

    /// <summary>
    /// Returns the generator registered under <paramref name="languageId"/>, or
    /// <see langword="null"/> when no such generator is registered.
    /// </summary>
    public static ILanguageGenerator? Resolve(string languageId)
    {
        ArgumentException.ThrowIfNullOrEmpty(languageId);
        return Generators.TryGetValue(languageId, out var gen) ? gen : null;
    }

    /// <summary>Snapshot of every currently registered generator.</summary>
    public static IReadOnlyCollection<ILanguageGenerator> All => Generators.Values.ToList();
}
