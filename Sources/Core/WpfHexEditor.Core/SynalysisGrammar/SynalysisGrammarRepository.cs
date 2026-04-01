// ==========================================================
// Project: WpfHexEditor.Core
// File: SynalysisGrammar/SynalysisGrammarRepository.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Loads and caches UfwbRoot grammar objects from disk files or embedded
//     assembly resources. Acts as the single authoritative lookup for grammar
//     selection by file extension.
//
// Architecture Notes:
//     Pattern: Repository + Cache
//     - All loaded grammars are cached by their key (path or resource name) in
//       a ConcurrentDictionary for thread-safe, lazy access.
//     - Extension lookup is built once on first use from all registered entries.
//     - Plugin-contributed grammars (via RegisterGrammar) override embedded ones
//       for the same extension — same priority model as WHFMTDetectionAdapter.
// ==========================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace WpfHexEditor.Core.SynalysisGrammar;

/// <summary>
/// Loads, caches, and resolves UFWB grammar files.
/// Supports both embedded assembly resources and disk files.
/// </summary>
public sealed class SynalysisGrammarRepository
{
    private readonly SynalysisGrammarParser                    _parser = new();
    private readonly ConcurrentDictionary<string, UfwbRoot>   _cache  = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Registered entries: key → UfwbRoot. Built from embedded + plugin contributions.</summary>
    private readonly List<GrammarRegistration> _registrations = [];

    /// <summary>Lazy extension→grammarKey lookup (built once on first access).</summary>
    private Lazy<Dictionary<string, string>> _extensionMap;

    public SynalysisGrammarRepository()
    {
        _extensionMap = new Lazy<Dictionary<string, string>>(BuildExtensionMap);
    }

    // -- Registration ------------------------------------------------------

    /// <summary>
    /// Registers an embedded assembly resource as a grammar.
    /// The <paramref name="assembly"/> must contain a manifest resource at
    /// <paramref name="resourceName"/>.
    /// </summary>
    public void RegisterEmbedded(Assembly assembly, string resourceName)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentException.ThrowIfNullOrEmpty(resourceName);

        _registrations.Add(new GrammarRegistration(resourceName, () => LoadFromEmbedded(assembly, resourceName)));
        InvalidateExtensionMap();
    }

    /// <summary>Registers a grammar file on disk.</summary>
    public void RegisterFile(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        _registrations.Add(new GrammarRegistration(path, () => LoadFromFile(path)));
        InvalidateExtensionMap();
    }

    /// <summary>
    /// Registers an already-parsed grammar directly (e.g. from a plugin contribution).
    /// If extensions of this grammar overlap with existing entries the new registration
    /// takes precedence (last-registered wins for runtime overrides).
    /// </summary>
    public void RegisterGrammar(string key, UfwbRoot grammar)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(grammar);

        _cache[key] = grammar;
        _registrations.Add(new GrammarRegistration(key, () => grammar));
        InvalidateExtensionMap();
    }

    // -- Lookup ------------------------------------------------------------

    /// <summary>
    /// Returns the grammar whose file extension list includes <paramref name="extension"/>,
    /// or null when none is registered for that extension.
    /// </summary>
    /// <param name="extension">File extension, e.g. ".png" or "png".</param>
    public UfwbRoot? FindByExtension(string extension)
    {
        if (string.IsNullOrEmpty(extension)) return null;

        var key = extension.StartsWith('.') ? extension.ToLowerInvariant() : "." + extension.ToLowerInvariant();

        if (!_extensionMap.Value.TryGetValue(key, out var grammarKey)) return null;

        return GetOrLoad(grammarKey);
    }

    /// <summary>
    /// Loads a grammar by its registration key (resource name or file path).
    /// Returns null on failure.
    /// </summary>
    public UfwbRoot? GetByKey(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        return GetOrLoad(key);
    }

    /// <summary>Returns a snapshot of all registered grammar keys.</summary>
    public IReadOnlyList<string> GetAllKeys()
        => _registrations.Select(r => r.Key).ToList();

    /// <summary>Returns all registered grammars as (Key, Name) entries.</summary>
    public IReadOnlyList<GrammarEntry> GetAll()
        => _registrations
            .Select(r => new GrammarEntry(r.Key, Path.GetFileNameWithoutExtension(r.Key)))
            .ToList();

    /// <summary>
    /// Returns the already-cached <see cref="UfwbRoot"/> for <paramref name="key"/>
    /// without triggering a lazy load. Returns null when the key is unknown or the
    /// grammar has not yet been loaded.
    /// Use <see cref="GetByKey"/> when you need on-demand loading.
    /// </summary>
    public UfwbRoot? GetParsedGrammar(string key)
        => _cache.TryGetValue(key, out var g) ? g : null;

    // -- Internal loading --------------------------------------------------

    private UfwbRoot? GetOrLoad(string key)
    {
        if (_cache.TryGetValue(key, out var cached)) return cached;

        var registration = _registrations.FirstOrDefault(r =>
            r.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

        if (registration is null) return null;

        try
        {
            var grammar = registration.Loader();
            if (grammar is not null)
                _cache[key] = grammar;
            return grammar;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SynalysisGrammarRepository] Failed to load '{key}': {ex.Message}");
            return null;
        }
    }

    private UfwbRoot? LoadFromFile(string path)
    {
        if (!File.Exists(path)) return null;
        return _parser.ParseFromFile(path);
    }

    private UfwbRoot? LoadFromEmbedded(Assembly assembly, string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null) return null;
        return _parser.ParseFromStream(stream);
    }

    // -- Extension map -----------------------------------------------------

    private Dictionary<string, string> BuildExtensionMap()
    {
        // Last-registration wins for duplicate extensions.
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var reg in _registrations)
        {
            var grammar = GetOrLoad(reg.Key);
            if (grammar is null) continue;

            foreach (var ext in grammar.Grammar.FileExtensions)
                map[ext] = reg.Key;
        }

        return map;
    }

    private void InvalidateExtensionMap()
        => _extensionMap = new Lazy<Dictionary<string, string>>(BuildExtensionMap);

    // -- Inner types -------------------------------------------------------

    private sealed record GrammarRegistration(string Key, Func<UfwbRoot?> Loader);
}

/// <summary>Lightweight descriptor returned by <see cref="SynalysisGrammarRepository.GetAll"/>.</summary>
public sealed record GrammarEntry(string Key, string Name);
