// ==========================================================
// Project: WpfHexEditor.Core
// File: SynalysisGrammar/SynalysisDetectionAdapter.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Adapter that resolves a preferred editor ID for a file whose extension is
//     covered by an embedded or plugin-contributed UFWB grammar (.grammar).
//     Mirrors the pattern of WHFMTDetectionAdapter (in WpfHexEditor.Editor.Core)
//     but lives in Core because it depends on SynalysisGrammarRepository.
//
// Architecture Notes:
//     Pattern: Adapter
//     - All UFWB binary formats map to "hex-editor" by default.
//     - Plugin-registered overrides (via RegisterExtension) take highest priority.
//     - Returns null when the extension is not covered; caller falls back to
//       WHFMTDetectionAdapter then IEditorRegistry.
// ==========================================================

using System;
using System.Collections.Generic;
using System.IO;

namespace WpfHexEditor.Core.SynalysisGrammar;

/// <summary>
/// Resolves the preferred editor ID for a file based on whether a UFWB grammar
/// covers its extension.
/// </summary>
public sealed class SynalysisDetectionAdapter
{
    private const string DefaultEditorId = "hex-editor";

    private readonly SynalysisGrammarRepository _repository;

    /// <summary>
    /// Runtime extension→editorId overrides contributed by plugins after startup.
    /// Key is lowercase dot-prefixed extension, e.g. ".xyz".
    /// </summary>
    private readonly Dictionary<string, string> _overrides
        = new(StringComparer.OrdinalIgnoreCase);

    /// <param name="repository">
    /// Repository pre-populated with embedded grammars.
    /// Shared with <c>SynalysisGrammarPlugin</c>.
    /// </param>
    public SynalysisDetectionAdapter(SynalysisGrammarRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    // -- Lookup ------------------------------------------------------------

    /// <summary>
    /// Returns the preferred editor ID for <paramref name="filePath"/> based on
    /// its extension, or <c>null</c> when no UFWB grammar covers it.
    /// </summary>
    public string? GetPreferredEditorId(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return null;

        var ext = Path.GetExtension(filePath);
        if (string.IsNullOrEmpty(ext)) return null;

        if (_overrides.TryGetValue(ext, out var overrideId))
            return overrideId;

        return _repository.FindByExtension(ext) is not null ? DefaultEditorId : null;
    }

    // -- Runtime overrides -------------------------------------------------

    /// <summary>
    /// Registers a runtime extension→editorId mapping (e.g. from a plugin).
    /// Overrides any repository-derived entry for the same extension.
    /// </summary>
    public void RegisterExtension(string extension, string editorId)
    {
        ArgumentException.ThrowIfNullOrEmpty(extension);
        ArgumentException.ThrowIfNullOrEmpty(editorId);

        var key = extension.StartsWith('.') ? extension.ToLowerInvariant()
                                            : "." + extension.ToLowerInvariant();
        _overrides[key] = editorId;
    }
}
