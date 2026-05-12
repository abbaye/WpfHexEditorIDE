// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Snippets/UserSnippetStore.cs
// Description:
//     JSON-backed store for user-defined code snippets. Snippets are
//     scoped by language id ("csharp", "vbnet", "*" = global) and
//     persisted to %AppData%/WpfHexEditor/snippets.json.
// Architecture: thin file-backed repository; loaded lazily on first
//                 access, saved synchronously on Update().
// ==========================================================

using System.IO;
using System.Text.Json;

namespace WpfHexEditor.Editor.CodeEditor.Snippets;

/// <summary>One stored snippet plus its language scope.</summary>
public sealed class StoredSnippet
{
    public string LanguageId  { get; set; } = "*";     // "*" = any language
    public string Trigger     { get; set; } = "";
    public string Body        { get; set; } = "";
    public string Description { get; set; } = "";
}

/// <summary>Persists user-defined snippets across sessions.</summary>
public sealed class UserSnippetStore
{
    private static readonly string DefaultPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WpfHexEditor", "snippets.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented        = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _path;
    private List<StoredSnippet>? _cache;

    public UserSnippetStore() : this(DefaultPath) { }
    public UserSnippetStore(string path) => _path = path;

    private readonly object _lock = new();

    /// <summary>Returns a defensive copy so callers can iterate concurrently with mutations.</summary>
    public IReadOnlyList<StoredSnippet> GetAll()
    {
        lock (_lock) return EnsureLoaded().ToList();
    }

    /// <summary>Returns snippets matching <paramref name="languageId"/> or the global scope.</summary>
    public IEnumerable<StoredSnippet> GetForLanguage(string languageId)
    {
        // Materialize before yield to avoid holding the lock during enumeration.
        var snapshot = GetAll();
        foreach (var s in snapshot)
            if (s.LanguageId == "*" || string.Equals(s.LanguageId, languageId, StringComparison.OrdinalIgnoreCase))
                yield return s;
    }

    public void Add(StoredSnippet snippet)
    {
        lock (_lock)
        {
            var list = EnsureLoaded();
            list.RemoveAll(s => SameKey(s, snippet));
            list.Add(snippet);
            SaveAll(list);
        }
    }

    public void Remove(string languageId, string trigger)
    {
        lock (_lock)
        {
            var list = EnsureLoaded();
            list.RemoveAll(s => s.LanguageId == languageId && s.Trigger == trigger);
            SaveAll(list);
        }
    }

    /// <summary>
    /// Atomic batch replace — writes the entire snippet list once instead of
    /// the O(N) per-row Remove+Add cycle. Caller passes the desired final
    /// state; we copy defensively before persisting.
    /// </summary>
    public void ReplaceAll(IEnumerable<StoredSnippet> snippets)
    {
        ArgumentNullException.ThrowIfNull(snippets);
        var list = snippets.Select(Clone).ToList();
        lock (_lock) SaveAll(list);
    }

    private static StoredSnippet Clone(StoredSnippet s) => new()
    {
        LanguageId  = s.LanguageId,
        Trigger     = s.Trigger,
        Body        = s.Body,
        Description = s.Description,
    };

    private List<StoredSnippet> EnsureLoaded() => _cache ??= Load();

    private static bool SameKey(StoredSnippet a, StoredSnippet b)
        => string.Equals(a.LanguageId, b.LanguageId, StringComparison.OrdinalIgnoreCase)
        && string.Equals(a.Trigger,    b.Trigger,    StringComparison.Ordinal);

    private List<StoredSnippet> Load()
    {
        try
        {
            if (!File.Exists(_path)) return new List<StoredSnippet>();
            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<List<StoredSnippet>>(json, JsonOptions)
                   ?? new List<StoredSnippet>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[UserSnippetStore] load failed: {ex.Message}");
            return new List<StoredSnippet>();
        }
    }

    private void SaveAll(List<StoredSnippet> list)
    {
        _cache = list;
        try
        {
            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(_path, JsonSerializer.Serialize(list, JsonOptions));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[UserSnippetStore] save failed: {ex.Message}");
        }
    }
}
