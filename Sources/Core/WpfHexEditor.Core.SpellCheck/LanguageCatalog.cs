// ==========================================================
// Project: WpfHexEditor.Core.SpellCheck
// File: LanguageCatalog.cs
// Description:
//     Loads language metadata from the embedded languages.json and merges
//     with a user-provided override file at %APPDATA%\WpfHexEditor\languages.json.
//     Replaces the static KnownLanguages dictionary in DictionaryManager.
// Architecture:
//     Embedded JSON is the authoritative source. User file can add new entries
//     or override repoPath/prefix for existing codes. Stopwords are merged
//     (union) so user can extend without replacing the built-in set.
// ==========================================================

using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WpfHexEditor.Core.SpellCheck;

/// <summary>Immutable language metadata entry from languages.json.</summary>
public sealed record LanguageEntry(
    string   Code,
    string   Display,
    string   RepoPath,
    string   Prefix,
    string[] Stopwords);

/// <summary>
/// Singleton catalog — loads embedded languages.json at first access.
/// User can extend via %APPDATA%\WpfHexEditor\languages.json.
/// </summary>
public static class LanguageCatalog
{
    private static readonly Lazy<IReadOnlyList<LanguageEntry>> _languages =
        new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    public static IReadOnlyList<LanguageEntry> Languages => _languages.Value;

    public static LanguageEntry? Get(string code) =>
        _byCode.Value.TryGetValue(code, out var e) ? e : null;

    private static readonly Lazy<Dictionary<string, LanguageEntry>> _byCode =
        new(() => Languages.ToDictionary(e => e.Code, StringComparer.OrdinalIgnoreCase),
            LazyThreadSafetyMode.ExecutionAndPublication);

    private static IReadOnlyList<LanguageEntry> Load()
    {
        var embedded = LoadEmbedded();
        var userPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WpfHexEditor", "languages.json");

        if (!File.Exists(userPath))
            return embedded;

        try
        {
            var userEntries = ParseJson(File.ReadAllText(userPath));
            return Merge(embedded, userEntries);
        }
        catch
        {
            return embedded;
        }
    }

    private static IReadOnlyList<LanguageEntry> LoadEmbedded()
    {
        var asm  = Assembly.GetExecutingAssembly();
        var name = asm.GetManifestResourceNames()
                      .First(n => n.EndsWith("languages.json", StringComparison.OrdinalIgnoreCase));
        using var stream = asm.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return ParseJson(reader.ReadToEnd());
    }

    private static IReadOnlyList<LanguageEntry> ParseJson(string json)
    {
        var doc  = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
        var list = new List<LanguageEntry>();
        foreach (var lang in doc.RootElement.GetProperty("languages").EnumerateArray())
        {
            list.Add(new LanguageEntry(
                Code:      lang.GetProperty("code").GetString()!,
                Display:   lang.GetProperty("display").GetString()!,
                RepoPath:  lang.GetProperty("repoPath").GetString()!,
                Prefix:    lang.GetProperty("prefix").GetString()!,
                Stopwords: lang.TryGetProperty("stopwords", out var sw)
                    ? [.. sw.EnumerateArray().Select(s => s.GetString()!)]
                    : []));
        }
        return list;
    }

    // User entries override built-in by code; stopwords are unioned.
    private static IReadOnlyList<LanguageEntry> Merge(
        IReadOnlyList<LanguageEntry> builtin,
        IReadOnlyList<LanguageEntry> user)
    {
        var dict = builtin.ToDictionary(e => e.Code, StringComparer.OrdinalIgnoreCase);
        foreach (var u in user)
        {
            if (dict.TryGetValue(u.Code, out var existing))
            {
                var merged = existing with
                {
                    Display   = u.Display.Length > 0 ? u.Display : existing.Display,
                    RepoPath  = u.RepoPath.Length > 0 ? u.RepoPath : existing.RepoPath,
                    Prefix    = u.Prefix.Length > 0 ? u.Prefix : existing.Prefix,
                    Stopwords = [.. existing.Stopwords.Union(u.Stopwords, StringComparer.OrdinalIgnoreCase)],
                };
                dict[u.Code] = merged;
            }
            else
            {
                dict[u.Code] = u;
            }
        }
        return [.. dict.Values.OrderBy(e => e.Display, StringComparer.CurrentCulture)];
    }
}
