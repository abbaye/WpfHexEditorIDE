// ==========================================================
// Project: WpfHexEditor.LSP
// File: LanguageDefinitionManager.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Central registry for language definitions (.whlang).
//     Loads built-in definitions from WpfHexEditor.Definitions (embedded assembly
//     resources), user-created definitions from the workspace, and plugin-added
//     definitions at runtime. Priority: UserCreated > Imported > BuiltIn.
//
// Architecture Notes:
//     Pattern: Registry + Factory
//     - EmbeddedSyntaxCatalog provides raw JSON streams for built-in .whlang files.
//     - WhlangParser converts JSON to LanguageDefinition objects (lazy, cached).
//     - AddLanguage() lets plugins inject definitions dynamically.
//     - Thread-safe reads; registrations protected by lock.
// ==========================================================

using System.Text.Json;
using System.Text.RegularExpressions;
using WpfHexEditor.Definitions;
using WpfHexEditor.LSP.Models;

namespace WpfHexEditor.LSP;

/// <summary>
/// Manages all language definitions available to the editor.
/// </summary>
public sealed class LanguageDefinitionManager
{
    private readonly EmbeddedSyntaxCatalog _catalog;
    private readonly object _lock = new();

    // Cache keyed by languageId (lower-case).
    private readonly Dictionary<string, LanguageDefinition> _byId
        = new(StringComparer.OrdinalIgnoreCase);

    // Extension → languageId map (highest-priority wins).
    private readonly Dictionary<string, string> _byExtension
        = new(StringComparer.OrdinalIgnoreCase);

    private bool _initialized;

    // -----------------------------------------------------------------------

    public LanguageDefinitionManager() : this(EmbeddedSyntaxCatalog.Instance) { }

    public LanguageDefinitionManager(EmbeddedSyntaxCatalog catalog)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    // -- Public API ---------------------------------------------------------

    /// <summary>
    /// Ensures all built-in language definitions are loaded.
    /// Called lazily on first use; safe to call multiple times.
    /// </summary>
    public void Initialize()
    {
        lock (_lock)
        {
            if (_initialized) return;
            _initialized = true;
            LoadBuiltIn();
        }
    }

    /// <summary>
    /// Returns the <see cref="LanguageDefinition"/> for the given <paramref name="languageId"/>,
    /// or <c>null</c> if not found.
    /// </summary>
    public LanguageDefinition? GetById(string languageId)
    {
        EnsureInitialized();
        lock (_lock)
            return _byId.TryGetValue(languageId, out var def) ? def : null;
    }

    /// <summary>
    /// Returns the best <see cref="LanguageDefinition"/> for a given file path
    /// based on its extension, or <c>null</c> if no mapping exists.
    /// </summary>
    public LanguageDefinition? GetByFilePath(string filePath)
    {
        EnsureInitialized();
        var ext = System.IO.Path.GetExtension(filePath);
        if (string.IsNullOrEmpty(ext)) return null;
        lock (_lock)
            return _byExtension.TryGetValue(ext, out var id) && _byId.TryGetValue(id, out var def)
                ? def : null;
    }

    /// <summary>Returns all registered language definitions sorted by name.</summary>
    public IReadOnlyList<LanguageDefinition> GetAll()
    {
        EnsureInitialized();
        lock (_lock)
            return [.. _byId.Values.OrderBy(d => d.Name)];
    }

    /// <summary>
    /// Registers a language definition from a plugin or user workspace.
    /// If a definition with the same <see cref="LanguageDefinition.Id"/> already exists,
    /// it is replaced only when the new definition has higher or equal priority.
    /// </summary>
    public void AddLanguage(LanguageDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        EnsureInitialized();
        lock (_lock)
            RegisterDefinition(definition);
    }

    // -- Private ------------------------------------------------------------

    private void EnsureInitialized()
    {
        if (!_initialized) Initialize();
    }

    private void LoadBuiltIn()
    {
        var assembly = typeof(EmbeddedSyntaxCatalog).Assembly;
        foreach (var entry in _catalog.GetAll())
        {
            try
            {
                using var stream = assembly.GetManifestResourceStream(entry.ResourceKey);
                if (stream is null) continue;

                var def = WhlangParser.Parse(stream, entry, LanguagePriority.BuiltIn);
                if (def is not null)
                    RegisterDefinition(def);
            }
            catch
            {
                // Skip malformed definitions — don't crash the IDE.
            }
        }
    }

    private void RegisterDefinition(LanguageDefinition def)
    {
        // Replace if same id and new priority >= existing.
        if (_byId.TryGetValue(def.Id, out var existing) && existing.Priority > def.Priority)
            return;

        _byId[def.Id] = def;

        foreach (var ext in def.Extensions)
        {
            var key = ext.StartsWith('.') ? ext.ToLowerInvariant() : "." + ext.ToLowerInvariant();
            // Only overwrite if new definition has higher or equal priority.
            if (!_byExtension.TryGetValue(key, out var existingId)
                || !_byId.TryGetValue(existingId, out var existingDef)
                || def.Priority >= existingDef.Priority)
            {
                _byExtension[key] = def.Id;
            }
        }
    }
}

// ---------------------------------------------------------------------------
// Internal .whlang JSON parser
// ---------------------------------------------------------------------------

internal static class WhlangParser
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static LanguageDefinition? Parse(
        Stream stream,
        EmbeddedSyntaxEntry entry,
        LanguagePriority priority)
    {
        using var reader = new System.IO.StreamReader(stream);
        var json = reader.ReadToEnd();

        JsonDocument doc;
        try { doc = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip }); }
        catch { return null; }

        using (doc)
        {
            var root = doc.RootElement;
            var name     = root.GetString("name")             ?? entry.Name;
            var category = root.GetString("category")         ?? entry.Category;
            var id       = BuildId(name);
            var exts     = ParseStringArray(root, "extensions");
            var lineCmt  = root.GetString("lineComment");
            var blkStart = root.GetString("blockCommentStart");
            var blkEnd   = root.GetString("blockCommentEnd");

            var (rules, keywords, operators) = ParseRules(root);

            return new LanguageDefinition
            {
                Id               = id,
                Name             = name,
                Category         = category,
                Priority         = priority,
                Extensions       = exts.Count > 0 ? exts : entry.Extensions.ToList(),
                LineComment      = lineCmt,
                BlockCommentStart = blkStart,
                BlockCommentEnd  = blkEnd,
                Rules            = rules,
                Keywords         = keywords,
                Operators        = operators,
                SourceKey        = entry.ResourceKey,
            };
        }
    }

    private static string BuildId(string name)
        => Regex.Replace(name.ToLowerInvariant(), @"[^a-z0-9]+", "");

    private static List<string> ParseStringArray(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var arr)) return [];
        if (arr.ValueKind != JsonValueKind.Array) return [];
        return [.. arr.EnumerateArray().Select(e => e.GetString() ?? "").Where(s => s.Length > 0)];
    }

    private static (List<LanguageRule> Rules, List<string> Keywords, List<string> Operators)
        ParseRules(JsonElement root)
    {
        var rules     = new List<LanguageRule>();
        var keywords  = new List<string>();
        var operators = new List<string>();

        if (!root.TryGetProperty("rules", out var rulesArr)
            || rulesArr.ValueKind != JsonValueKind.Array)
            return (rules, keywords, operators);

        foreach (var r in rulesArr.EnumerateArray())
        {
            var type     = r.GetString("type")     ?? string.Empty;
            var pattern  = r.GetString("pattern")  ?? string.Empty;
            var colorKey = r.GetString("colorKey") ?? string.Empty;
            var isBold   = r.TryGetProperty("bold",   out var b) && b.GetBoolean();
            var isItalic = r.TryGetProperty("italic", out var i) && i.GetBoolean();

            if (string.IsNullOrEmpty(pattern)) continue;

            rules.Add(new LanguageRule(type, pattern, colorKey, isBold, isItalic));

            // Extract keyword list for fast completion.
            if (type.Equals("Keyword", StringComparison.OrdinalIgnoreCase))
                ExtractAlternatives(pattern, keywords);
            else if (type.Equals("Operator", StringComparison.OrdinalIgnoreCase))
                ExtractAlternatives(pattern, operators);
        }

        return (rules, keywords, operators);
    }

    /// <summary>
    /// Extracts literal alternatives from a simple alternation pattern like
    /// <c>\\b(if|else|while)\\b</c> or <c>(+|-|\*)</c>.
    /// </summary>
    private static void ExtractAlternatives(string pattern, List<string> target)
    {
        var m = Regex.Match(pattern, @"\(([^)]+)\)");
        if (!m.Success) return;
        foreach (var part in m.Groups[1].Value.Split('|'))
        {
            var word = Regex.Replace(part, @"\\[bB]|\\[wW]|\[.*?\]", "").Trim();
            if (word.Length > 0 && !word.StartsWith('\\'))
                target.Add(word);
        }
    }
}

/// <summary>Extension helpers for <see cref="JsonElement"/>.</summary>
internal static class JsonElementExtensions
{
    public static string? GetString(this JsonElement el, string property)
        => el.TryGetProperty(property, out var p) && p.ValueKind == JsonValueKind.String
           ? p.GetString() : null;
}
