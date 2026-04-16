//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Collections.Frozen;
using System.Reflection;
using System.Text.Json;
using WpfHexEditor.Core.Contracts;

namespace WpfHexEditor.Core.Definitions;

/// <summary>
/// Singleton catalog of the embedded <c>.whfmt</c> format definitions
/// shipped inside <c>WpfHexEditor.Core.Definitions.dll</c>.
/// <para>
/// On first call to <see cref="GetAll"/> the catalog performs a lazy scan of
/// all manifest resources matching the pattern
/// <c>WpfHexEditor.Core.Definitions.FormatDefinitions.*.whfmt</c> and extracts
/// lightweight header information without loading the full block definitions.
/// </para>
/// </summary>
public sealed class EmbeddedFormatCatalog : IEmbeddedFormatCatalog
{
    // -- Singleton -------------------------------------------------------------

    // JSONC support: .whfmt files contain // comment headers — skip them during parse.
    private static readonly JsonDocumentOptions s_jsonOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>
    /// The singleton instance.
    /// </summary>
    public static EmbeddedFormatCatalog Instance => LazyInitializer.EnsureInitialized(ref field, () => new EmbeddedFormatCatalog());

    private EmbeddedFormatCatalog() { }

    // -- Lazy cache ------------------------------------------------------------
    private IReadOnlySet<EmbeddedFormatEntry> Entries => LazyInitializer.EnsureInitialized(ref field, () => MakeEntries());
    private IReadOnlySet<string> Categories => LazyInitializer.EnsureInitialized(ref field, () => MakeCategories(Entries));

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
    public IReadOnlySet<EmbeddedFormatEntry> GetAll() => Entries;

    public static IReadOnlySet<EmbeddedFormatEntry> MakeEntries(bool rethrow = false)
    {
        var list = new List<EmbeddedFormatEntry>();
        foreach (var key in DefinitionsAssembly.GetManifestResourceNames())
        {
            if (!key.Contains("FormatDefinitions")) continue;
            var isWhfmt = key.EndsWith(".whfmt");
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
                if (rethrow)
                    throw;
                // Skip malformed resources
            }
        }

        list.Sort((a, b) =>
        {
            var cat = string.Compare(a.Category, b.Category, StringComparison.OrdinalIgnoreCase);
            return cat != 0 ? cat : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        });

        return list.ToFrozenSet();
    }

    /// <inheritdoc/>
    public IReadOnlySet<string> GetCategories() => Categories;

    public static IReadOnlySet<string> MakeCategories(IReadOnlySet<EmbeddedFormatEntry> entries)
    {
        return entries.Select(e => e.Category)
                      .Distinct(StringComparer.OrdinalIgnoreCase)
                      .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
                      .ToFrozenSet();
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

    /// <inheritdoc/>
    public IReadOnlyList<string> GetCompatibleEditorIds(string filePath)
    {
        var ext = Path.GetExtension(filePath)?.ToLowerInvariant();
        if (string.IsNullOrEmpty(ext)) return [];

        var entry = GetByExtension(ext);
        if (entry is null) return [];

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "hex-editor"   // always compatible
        };

        if (entry.PreferredEditor is not null)
            ids.Add(entry.PreferredEditor);

        if (entry.IsTextFormat)
        {
            ids.Add("code-editor");
            ids.Add("text-editor");
        }

        switch (entry.Category)
        {
            case "Images": ids.Add("image-viewer");  break;
            case "Audio":  ids.Add("audio-viewer");  break;
        }

        if (entry.PreferredEditor == "structure-editor")
            ids.Add("structure-editor");

        if (entry.DiffMode == "text")
            ids.Add("diff-viewer");

        return [.. ids];
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

    // -- New public API --------------------------------------------------------

    /// <inheritdoc/>
    public IReadOnlyList<EmbeddedFormatEntry> GetByCategory(string category)
        => GetAll()
            .Where(e => e.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
            .ToList();

    /// <inheritdoc/>
    public EmbeddedFormatEntry? DetectFromBytes(ReadOnlySpan<byte> header)
    {
        EmbeddedFormatEntry? best = null;
        double bestScore = 0;

        foreach (var entry in GetAll())
        {
            if (entry.Signatures is not { Count: > 0 }) continue;
            double score = 0;
            foreach (var sig in entry.Signatures)
            {
                var bytes = Convert.FromHexString(sig.Value);
                if (sig.Offset + bytes.Length > header.Length) continue;
                if (header.Slice(sig.Offset, bytes.Length).SequenceEqual(bytes))
                    score += sig.Weight;
            }
            if (score > bestScore) { bestScore = score; best = entry; }
        }
        return bestScore > 0 ? best : null;
    }

    /// <inheritdoc/>
    public EmbeddedFormatEntry? GetByMimeType(string mimeType)
        => GetAll().FirstOrDefault(e =>
            e.MimeTypes?.Any(m => m.Equals(mimeType, StringComparison.OrdinalIgnoreCase)) == true);

    /// <inheritdoc/>
    public string? GetSchemaJson(string schemaName)
    {
        var key = DefinitionsAssembly
            .GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith($"{schemaName}.schema.json", StringComparison.OrdinalIgnoreCase));
        if (key is null) return null;
        using var stream = DefinitionsAssembly.GetManifestResourceStream(key)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
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

        // MimeTypes
        var mimeTypes = new List<string>();
        if (root.TryGetProperty("MimeTypes", out var mt) && mt.ValueKind == JsonValueKind.Array)
            foreach (var m in mt.EnumerateArray())
                if (m.GetString() is { } ms) mimeTypes.Add(ms);

        // Signatures (detection.signatures)
        var signatures = new List<FormatSignature>();
        if (root.TryGetProperty("detection", out var det2) &&
            det2.TryGetProperty("signatures", out var sigs) &&
            sigs.ValueKind == JsonValueKind.Array)
            foreach (var sig in sigs.EnumerateArray())
            {
                var val    = sig.TryGetProperty("value",  out var v) ? v.GetString() ?? "" : "";
                var off    = sig.TryGetProperty("offset", out var o) ? o.GetInt32()       : 0;
                var weight = sig.TryGetProperty("weight", out var w) ? w.GetDouble()      : 1.0;
                if (val.Length > 0) signatures.Add(new FormatSignature(val, off, weight));
            }

        return new EmbeddedFormatEntry(
            resourceKey, name, category, description, extensions,
            quality, version, author, platform, preferredEditor,
            isTextFormat, hasSyntaxDef, diffMode,
            mimeTypes.Count > 0 ? mimeTypes : null,
            signatures.Count > 0 ? signatures : null);
    }

    private static string? GetString(JsonElement root, string property)
    {
        if (root.TryGetProperty(property, out var el) && el.ValueKind == JsonValueKind.String)
            return el.GetString();
        return null;
    }

    /// <summary>
    /// Extracts the category from a resource key like
    /// <c>WpfHexEditor.Core.Definitions.FormatDefinitions.Archives.ZIP.whfmt</c> → <c>Archives</c>.
    /// </summary>
    private static string ExtractCategoryFromKey(string key)
    {
        const string prefix = "WpfHexEditor.Core.Definitions.FormatDefinitions.";
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
