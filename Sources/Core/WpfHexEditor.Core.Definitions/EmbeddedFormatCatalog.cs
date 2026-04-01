//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.Core.Definitions;

/// <summary>
/// Singleton catalog of the embedded <c>.whfmt</c> format definitions
/// shipped inside <c>WpfHexEditor.Definitions.dll</c>.
/// <para>
/// On first call to <see cref="GetAll"/> the catalog performs a lazy scan of
/// all manifest resources matching the pattern
/// <c>WpfHexEditor.Definitions.FormatDefinitions.*.whfmt</c> and extracts
/// lightweight header information without loading the full block definitions.
/// </para>
/// </summary>
public sealed class EmbeddedFormatCatalog : IEmbeddedFormatCatalog
{
    // -- Singleton -------------------------------------------------------------

    private static EmbeddedFormatCatalog? _instance;

    // JSONC support: .whfmt files contain // comment headers — skip them during parse.
    private static readonly JsonDocumentOptions s_jsonOptions = new()
    {
        CommentHandling    = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>
    /// The singleton instance.
    /// </summary>
    public static EmbeddedFormatCatalog Instance
        => _instance ??= new EmbeddedFormatCatalog();

    private EmbeddedFormatCatalog() { }

    // -- Lazy cache ------------------------------------------------------------

    private IReadOnlyList<EmbeddedFormatEntry>? _entries;
    private IReadOnlyList<string>?              _categories;

    /// <summary>
    /// Thread-safe cache: embedded resource key → raw JSON text.
    /// Populated on first <see cref="GetJson"/> call per key; all subsequent
    /// calls return the cached string without re-opening the assembly stream.
    /// </summary>
    private readonly Dictionary<string, string> _jsonCache
        = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _jsonCacheLock = new();

    private static readonly Assembly DefinitionsAssembly =
        typeof(EmbeddedFormatCatalog).Assembly;

    // -- IEmbeddedFormatCatalog ------------------------------------------------

    /// <inheritdoc/>
    public IReadOnlyList<EmbeddedFormatEntry> GetAll()
    {
        if (_entries is not null) return _entries;

        var list = new List<EmbeddedFormatEntry>();
        foreach (var key in DefinitionsAssembly.GetManifestResourceNames())
        {
            if (!key.Contains("FormatDefinitions")) continue;
            var isWhfmt   = key.EndsWith(".whfmt");
            var isGrammar = key.EndsWith(".grammar");
            if (!isWhfmt && !isGrammar) continue;

            try
            {
                EmbeddedFormatEntry? entry = isGrammar
                    ? LoadGrammarHeader(key)
                    : LoadHeader(key);
                if (entry is not null) list.Add(entry);
            }
            catch
            {
                // Skip malformed resources
            }
        }

        list.Sort((a, b) =>
        {
            var cat = string.Compare(a.Category, b.Category, StringComparison.OrdinalIgnoreCase);
            return cat != 0 ? cat : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        });

        _entries    = list;
        _categories = list.Select(e => e.Category)
                          .Distinct(StringComparer.OrdinalIgnoreCase)
                          .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
                          .ToList();
        return _entries;
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> GetCategories()
    {
        GetAll(); // ensure cache is populated
        return _categories!;
    }

    /// <inheritdoc/>
    public string GetJson(string resourceKey)
    {
        lock (_jsonCacheLock)
        {
            if (_jsonCache.TryGetValue(resourceKey, out var cached))
                return cached;
        }

        // Read outside the lock — stream is independent per call.
        using var stream = DefinitionsAssembly.GetManifestResourceStream(resourceKey)
            ?? throw new InvalidOperationException($"Resource not found: {resourceKey}");
        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();

        lock (_jsonCacheLock)
            _jsonCache.TryAdd(resourceKey, json);

        return json;
    }

    /// <inheritdoc/>
    public EmbeddedFormatEntry? GetByExtension(string extension)
    {
        if (string.IsNullOrEmpty(extension)) return null;
        var ext = extension.StartsWith('.') ? extension : '.' + extension;
        return GetAll().FirstOrDefault(e =>
            e.Extensions.Any(x => x.Equals(ext, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Pre-warms the JSON cache for all embedded format entries by reading
    /// every resource key into <see cref="_jsonCache"/>.
    /// Safe to call from a background thread at application startup so that
    /// the first <see cref="HexEditor"/> creation never blocks the UI thread
    /// on stream I/O.
    /// </summary>
    public void PreWarm()
    {
        foreach (var entry in GetAll())
        {
            try { GetJson(entry.ResourceKey); }
            catch { /* skip malformed entries */ }
        }
    }

    // -- Public API (syntaxDefinition) ----------------------------------------

    /// <summary>
    /// Extracts the raw JSON text of the <c>syntaxDefinition</c> block from the .whfmt
    /// resource identified by <paramref name="resourceKey"/>.
    /// Returns <see langword="null"/> when the resource has no syntaxDefinition block.
    /// <para>
    /// Callers that have access to <c>LanguageDefinitionSerializer</c> should pass
    /// the returned JSON to
    /// <c>LanguageDefinitionSerializer.ParseSyntaxDefinitionBlock()</c>.
    /// </para>
    /// </summary>
    public string? GetSyntaxDefinitionJson(string resourceKey)
    {
        using var stream = DefinitionsAssembly.GetManifestResourceStream(resourceKey);
        if (stream is null) return null;

        using var doc  = JsonDocument.Parse(stream, s_jsonOptions);
        var root = doc.RootElement;

        if (!root.TryGetProperty("syntaxDefinition", out var syntaxBlock)) return null;

        return syntaxBlock.GetRawText();
    }

    // -- Private helpers -------------------------------------------------------

    private static EmbeddedFormatEntry? LoadHeader(string resourceKey)
    {
        using var stream = DefinitionsAssembly.GetManifestResourceStream(resourceKey);
        if (stream is null) return null;

        using var doc = JsonDocument.Parse(stream, s_jsonOptions);
        var root = doc.RootElement;

        var name        = GetString(root, "formatName") ?? ExtractNameFromKey(resourceKey);
        var description = GetString(root, "description") ?? "";
        var category    = GetString(root, "category")    ?? ExtractCategoryFromKey(resourceKey);
        var version     = GetString(root, "version")     ?? "";
        var author      = GetString(root, "author")      ?? "";
        int quality     = 0;

        if (root.TryGetProperty("QualityMetrics", out var qm) &&
            qm.TryGetProperty("CompletenessScore", out var qs))
            quality = qs.GetInt32();

        var extensions = new List<string>();
        if (root.TryGetProperty("extensions", out var ext) && ext.ValueKind == JsonValueKind.Array)
            foreach (var e in ext.EnumerateArray())
                if (e.GetString() is { } s) extensions.Add(s);

        // Extract platform from TechnicalDetails (present in ROM format definitions)
        var platform = "";
        if (root.TryGetProperty("TechnicalDetails", out var td))
            platform = GetString(td, "Platform") ?? "";

        // Preferred editor hint (optional — guides initial editor selection in MainWindow)
        var preferredEditor = GetString(root, "preferredEditor");

        // Text-format flag from detection rule (fallback derivation for editor selection)
        var isTextFormat = false;
        if (root.TryGetProperty("detection", out var det) &&
            det.TryGetProperty("isTextFormat", out var itf) &&
            itf.ValueKind == JsonValueKind.True)
            isTextFormat = true;

        // Detect presence of a syntaxDefinition block for language registration.
        bool hasSyntaxDef = root.TryGetProperty("syntaxDefinition", out _);

        // Preferred diff mode declared at the .whfmt root ("text", "semantic", "binary").
        var diffMode = GetString(root, "diffMode");

        return new EmbeddedFormatEntry(resourceKey, name, category, description, extensions, quality, version, author, platform, preferredEditor, isTextFormat, hasSyntaxDef, diffMode);
    }

    private static string? GetString(JsonElement root, string property)
    {
        if (root.TryGetProperty(property, out var el) && el.ValueKind == JsonValueKind.String)
            return el.GetString();
        return null;
    }

    /// <summary>
    /// Extracts the category from a resource key like
    /// <c>WpfHexEditor.Definitions.FormatDefinitions.Archives.ZIP.whfmt</c> → <c>Archives</c>.
    /// </summary>
    private static string ExtractCategoryFromKey(string key)
    {
        const string prefix = "WpfHexEditor.Definitions.FormatDefinitions.";
        if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            var rest   = key.Substring(prefix.Length);  // "Archives.ZIP.whfmt"
            var dotIdx = rest.IndexOf('.');
            if (dotIdx > 0) return rest.Substring(0, dotIdx);
        }
        return "Other";
    }

    private static string ExtractNameFromKey(string key)
    {
        // Last segment before ".whfmt" or ".grammar"
        var parts = key.Split('.');
        return parts.Length >= 2 ? parts[parts.Length - 2] : key;
    }

    /// <summary>
    /// Loads a lightweight header entry from an embedded UFWB <c>.grammar</c> XML resource.
    /// Extracts name, author, fileextension, and category from the XML attributes without
    /// deserialising the full element tree.
    /// </summary>
    private static EmbeddedFormatEntry? LoadGrammarHeader(string resourceKey)
    {
        using var stream = DefinitionsAssembly.GetManifestResourceStream(resourceKey);
        if (stream is null) return null;

        // Lightweight XML parsing — only reads the <grammar> element attributes.
        using var reader = System.Xml.XmlReader.Create(stream, new System.Xml.XmlReaderSettings { IgnoreWhitespace = true });
        while (reader.Read())
        {
            if (reader.NodeType != System.Xml.XmlNodeType.Element) continue;
            if (reader.LocalName != "grammar") continue;

            var name        = reader.GetAttribute("name")          ?? ExtractNameFromKey(resourceKey);
            var author      = reader.GetAttribute("author")        ?? "";
            var fileExt     = reader.GetAttribute("fileextension") ?? "";
            var description = "";

            // Try to read <description> child text.
            if (reader.Read() && reader.NodeType == System.Xml.XmlNodeType.Element
                && reader.LocalName == "description" && reader.Read())
                description = reader.Value.Trim();

            var extensions = fileExt
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(e => e.StartsWith('.') ? e.ToLowerInvariant() : "." + e.ToLowerInvariant())
                .ToList();

            var category = ExtractCategoryFromKey(resourceKey);

            return new EmbeddedFormatEntry(
                resourceKey, name, category, description,
                extensions, QualityScore: 70,
                Version: "", Author: author,
                Platform: "", PreferredEditor: "hex-editor",
                IsTextFormat: false, HasSyntaxDefinition: false, DiffMode: null);
        }

        return null;
    }
}
