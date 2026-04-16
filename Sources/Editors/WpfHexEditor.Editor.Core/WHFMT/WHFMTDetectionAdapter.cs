// ==========================================================
// Project: WpfHexEditor.Editor.Core
// File: WHFMT/WHFMTDetectionAdapter.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Adapter that resolves a preferred editor ID for a given file path
//     by querying the IEmbeddedFormatCatalog — the authoritative source
//     of .whfmt definitions (loaded as embedded assembly resources).
//     Plugin-registered mappings can override catalog entries at runtime.
//
// Architecture Notes:
//     Pattern: Adapter
//     - Delegates to IEmbeddedFormatCatalog.GetAll() to build the extension
//       lookup table on first use (lazy, thread-safe via Lazy<T>).
//     - RegisterExtension() adds plugin-contributed overrides on top of the
//       catalog-derived mappings.
//     - Returns null when neither the catalog nor any override covers the
//       file extension; the caller falls back to IEditorRegistry.
// ==========================================================

using WpfHexEditor.Core.Contracts;

namespace WpfHexEditor.Editor.Core.WHFMT;

/// <summary>
/// Resolves the preferred editor ID for a file by consulting the
/// embedded .whfmt format catalog (<see cref="IEmbeddedFormatCatalog"/>).
/// Plugin-registered overrides take precedence over catalog entries.
/// Returns <c>null</c> when no mapping is found.
/// </summary>
public sealed class WHFMTDetectionAdapter
{
    private readonly IEmbeddedFormatCatalog _catalog;

    /// <summary>
    /// Runtime overrides contributed by plugins after startup.
    /// Key is lowercase dot-prefixed extension (e.g. ".xyz").
    /// </summary>
    private readonly Dictionary<string, string> _overrides = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Lazy extension→editorId map built from the catalog on first use.
    /// </summary>
    private readonly Lazy<Dictionary<string, string>> _catalogMap;

    /// <summary>
    /// Initialises the adapter with the provided format catalog.
    /// </summary>
    /// <param name="catalog">
    /// Catalog to query. Pass <c>EmbeddedFormatCatalog.Instance</c> in production.
    /// </param>
    public WHFMTDetectionAdapter(IEmbeddedFormatCatalog catalog)
    {
        _catalog    = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _catalogMap = new Lazy<Dictionary<string, string>>(BuildCatalogMap);
    }

    // -- Lookup -----------------------------------------------------------

    /// <summary>
    /// Returns the preferred editor ID for <paramref name="filePath"/> based on
    /// its extension, or <c>null</c> when no mapping is found.
    /// </summary>
    public string? GetPreferredEditorId(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return null;

        var ext = System.IO.Path.GetExtension(filePath);
        if (string.IsNullOrEmpty(ext)) return null;

        // Plugin overrides take highest priority.
        if (_overrides.TryGetValue(ext, out var overrideId))
            return overrideId;

        // Fall back to catalog-derived mappings.
        return _catalogMap.Value.TryGetValue(ext, out var catalogId) ? catalogId : null;
    }

    // -- Runtime overrides ------------------------------------------------

    /// <summary>
    /// Registers a runtime extension→editor mapping (e.g. from a plugin).
    /// Overrides any catalog-derived entry for the same extension.
    /// </summary>
    /// <param name="extension">File extension with or without leading dot.</param>
    /// <param name="editorId">Target editor ID (e.g. "hex-editor").</param>
    public void RegisterExtension(string extension, string editorId)
    {
        ArgumentException.ThrowIfNullOrEmpty(extension);
        ArgumentException.ThrowIfNullOrEmpty(editorId);

        var key = extension.StartsWith('.') ? extension.ToLowerInvariant()
                                            : "." + extension.ToLowerInvariant();
        _overrides[key] = editorId;
    }

    /// <summary>Returns the number of extension mappings currently available (catalog + overrides).</summary>
    public int MappingCount => _catalogMap.Value.Count + _overrides.Count;

    // -- Internal ---------------------------------------------------------

    private Dictionary<string, string> BuildCatalogMap()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in _catalog.GetAll())
        {
            if (string.IsNullOrWhiteSpace(entry.PreferredEditor)) continue;

            foreach (var ext in entry.Extensions)
            {
                if (string.IsNullOrWhiteSpace(ext)) continue;

                var key = ext.StartsWith('.') ? ext.ToLowerInvariant()
                                              : "." + ext.ToLowerInvariant();

                // First definition wins when multiple formats share an extension.
                map.TryAdd(key, entry.PreferredEditor);
            }
        }

        return map;
    }
}
