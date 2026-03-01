//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.Core.Services;

/// <summary>
/// Singleton catalog of the embedded <c>.whjson</c> format definitions
/// shipped inside <c>WpfHexEditor.Core.dll</c>.
/// <para>
/// On first call to <see cref="GetAll"/> the catalog performs a lazy scan of
/// all manifest resources matching the pattern
/// <c>WpfHexEditor.Core.FormatDefinitions.*.whjson</c> and extracts
/// lightweight header information without loading the full block definitions.
/// </para>
/// </summary>
public sealed class EmbeddedFormatCatalog : IEmbeddedFormatCatalog
{
    // ── Singleton ─────────────────────────────────────────────────────────────

    private static EmbeddedFormatCatalog? _instance;

    /// <summary>The singleton instance.</summary>
    public static EmbeddedFormatCatalog Instance
        => _instance ??= new EmbeddedFormatCatalog();

    private EmbeddedFormatCatalog() { }

    // ── Lazy cache ────────────────────────────────────────────────────────────

    private IReadOnlyList<EmbeddedFormatEntry>? _entries;
    private IReadOnlyList<string>?              _categories;

    private static readonly Assembly CoreAssembly =
        typeof(EmbeddedFormatCatalog).Assembly;

    // ── IEmbeddedFormatCatalog ────────────────────────────────────────────────

    /// <inheritdoc/>
    public IReadOnlyList<EmbeddedFormatEntry> GetAll()
    {
        if (_entries is not null) return _entries;

        var list = new List<EmbeddedFormatEntry>();
        foreach (var key in CoreAssembly.GetManifestResourceNames())
        {
            if (!key.Contains("FormatDefinitions") || !key.EndsWith(".whjson"))
                continue;

            try
            {
                var entry = LoadHeader(key);
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
        using var stream = CoreAssembly.GetManifestResourceStream(resourceKey)
            ?? throw new InvalidOperationException($"Resource not found: {resourceKey}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static EmbeddedFormatEntry? LoadHeader(string resourceKey)
    {
        using var stream = CoreAssembly.GetManifestResourceStream(resourceKey);
        if (stream is null) return null;

        using var doc = JsonDocument.Parse(stream);
        var root = doc.RootElement;

        var name        = GetString(root, "formatName") ?? ExtractNameFromKey(resourceKey);
        var description = GetString(root, "description") ?? "";
        var category    = GetString(root, "category")    ?? ExtractCategoryFromKey(resourceKey);
        int quality     = 0;

        if (root.TryGetProperty("QualityMetrics", out var qm) &&
            qm.TryGetProperty("CompletenessScore", out var qs))
            quality = qs.GetInt32();

        var extensions = new List<string>();
        if (root.TryGetProperty("extensions", out var ext) && ext.ValueKind == JsonValueKind.Array)
            foreach (var e in ext.EnumerateArray())
                if (e.GetString() is { } s) extensions.Add(s);

        return new EmbeddedFormatEntry(resourceKey, name, category, description, extensions, quality);
    }

    private static string? GetString(JsonElement root, string property)
    {
        if (root.TryGetProperty(property, out var el) && el.ValueKind == JsonValueKind.String)
            return el.GetString();
        return null;
    }

    /// <summary>
    /// Extracts the category from a resource key like
    /// <c>WpfHexEditor.Core.FormatDefinitions.Archives.ZIP.whjson</c> → <c>Archives</c>.
    /// </summary>
    private static string ExtractCategoryFromKey(string key)
    {
        // Strip prefix and suffix, split on '.'
        const string prefix = "WpfHexEditor.Core.FormatDefinitions.";
        if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            var rest   = key.Substring(prefix.Length);  // "Archives.ZIP.whjson"
            var dotIdx = rest.IndexOf('.');
            if (dotIdx > 0) return rest.Substring(0, dotIdx);
        }
        return "Other";
    }

    private static string ExtractNameFromKey(string key)
    {
        // Last segment before ".whjson"
        var parts = key.Split('.');
        return parts.Length >= 2 ? parts[parts.Length - 2] : key;
    }
}
