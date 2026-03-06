//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace WpfHexEditor.Definitions;

/// <summary>
/// Singleton catalog of the embedded <c>.whlang</c> syntax definitions
/// shipped inside <c>WpfHexEditor.Definitions.dll</c>.
/// <para>
/// Returns lightweight <see cref="EmbeddedSyntaxEntry"/> metadata records plus
/// raw JSON streams; parsing into <c>SyntaxDefinition</c> objects is delegated
/// to <c>WpfHexEditor.Editor.TextEditor</c> which owns the highlighting model.
/// </para>
/// </summary>
public sealed class EmbeddedSyntaxCatalog
{
    // ── Singleton ─────────────────────────────────────────────────────────────

    private static EmbeddedSyntaxCatalog? _instance;

    /// <summary>
    /// The singleton instance.
    /// </summary>
    public static EmbeddedSyntaxCatalog Instance
        => _instance ??= new EmbeddedSyntaxCatalog();

    private EmbeddedSyntaxCatalog() { }

    // ── Lazy cache ────────────────────────────────────────────────────────────

    private IReadOnlyList<EmbeddedSyntaxEntry>? _all;

    private static readonly Assembly DefinitionsAssembly =
        typeof(EmbeddedSyntaxCatalog).Assembly;

    private const string Prefix = "WpfHexEditor.Definitions.SyntaxDefinitions.";

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns all embedded syntax definitions sorted by name.
    /// </summary>
    public IReadOnlyList<EmbeddedSyntaxEntry> GetAll()
    {
        if (_all is not null) return _all;

        var list = new List<EmbeddedSyntaxEntry>();
        foreach (var key in DefinitionsAssembly.GetManifestResourceNames())
        {
            if (!key.Contains("SyntaxDefinitions") || !key.EndsWith(".whlang", StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                var entry = ParseHeader(key);
                if (entry is not null) list.Add(entry);
            }
            catch
            {
                // Skip malformed resources
            }
        }

        _all = [.. list.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)];
        return _all;
    }

    /// <summary>
    /// Opens the raw JSON stream for the given resource key.
    /// Caller is responsible for disposing the returned stream.
    /// Returns <see langword="null"/> if the key does not exist.
    /// </summary>
    public Stream? GetStream(string resourceKey)
        => DefinitionsAssembly.GetManifestResourceStream(resourceKey);

    /// <summary>
    /// Returns the full JSON text for the given resource key.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the resource key does not exist.</exception>
    public string GetContent(string resourceKey)
    {
        using var stream = GetStream(resourceKey)
            ?? throw new InvalidOperationException($"Resource not found: {resourceKey}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Finds the first entry whose <see cref="EmbeddedSyntaxEntry.Extensions"/>
    /// contains <paramref name="ext"/> (case-insensitive, leading dot required).
    /// </summary>
    public EmbeddedSyntaxEntry? FindByExtension(string? ext)
    {
        if (string.IsNullOrEmpty(ext)) return null;
        ext = ext!.ToLowerInvariant();
        return GetAll().FirstOrDefault(e => e.Extensions.Contains(ext));
    }

    /// <summary>
    /// Finds an entry by its display name (case-insensitive).
    /// </summary>
    public EmbeddedSyntaxEntry? FindByName(string? name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        return GetAll().FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static EmbeddedSyntaxEntry? ParseHeader(string resourceKey)
    {
        using var stream = DefinitionsAssembly.GetManifestResourceStream(resourceKey);
        if (stream is null) return null;

        using var doc = JsonDocument.Parse(stream);
        var root = doc.RootElement;

        var name     = GetString(root, "name")     ?? ExtractNameFromKey(resourceKey);
        var category = GetString(root, "category") ?? ExtractCategoryFromKey(resourceKey);

        var extensions = new List<string>();
        if (root.TryGetProperty("extensions", out var ext) && ext.ValueKind == JsonValueKind.Array)
            foreach (var e in ext.EnumerateArray())
                if (e.GetString() is { } s) extensions.Add(s);

        return new EmbeddedSyntaxEntry(resourceKey, name, category, extensions);
    }

    private static string? GetString(JsonElement root, string property)
    {
        if (root.TryGetProperty(property, out var el) && el.ValueKind == JsonValueKind.String)
            return el.GetString();
        return null;
    }

    private static string ExtractCategoryFromKey(string key)
    {
        if (key.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            var rest   = key.Substring(Prefix.Length);  // "CLike.csharp.whlang"
            var dotIdx = rest.IndexOf('.');
            if (dotIdx > 0) return rest.Substring(0, dotIdx);
        }
        return "Other";
    }

    private static string ExtractNameFromKey(string key)
    {
        var parts = key.Split('.');
        return parts.Length >= 2 ? parts[parts.Length - 2] : key;
    }
}
