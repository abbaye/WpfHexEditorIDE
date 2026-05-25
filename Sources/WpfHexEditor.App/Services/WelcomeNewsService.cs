// ==========================================================
// Project: WpfHexEditor.App
// File: Services/WelcomeNewsService.cs
// Description:
//     Fetches news feed items for the WelcomePanel.
//     Primary: fetches news-feed.json from GitHub (5s timeout).
//     Fallback: parses CHANGELOG.md from GitHub using the existing regex pipeline.
//     Results are cached in-memory for 30 minutes to avoid re-fetching on every panel open.
// ==========================================================

using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using WpfHexEditor.App.Controls.Welcome;

namespace WpfHexEditor.App.Services;

internal sealed class WelcomeNewsService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(5) };

    private const string NewsFeedUrl =
        "https://raw.githubusercontent.com/abbaye/WpfHexEditorControl/master/docs/news-feed.json";

    private const string ChangelogUrl =
        "https://raw.githubusercontent.com/abbaye/WpfHexEditorControl/master/docs/CHANGELOG.md";

    private IReadOnlyList<WelcomeNewsItem>? _cache;
    private DateTime _cacheExpiry = DateTime.MinValue;

    public async Task<IReadOnlyList<WelcomeNewsItem>> GetNewsAsync(CancellationToken ct = default)
    {
        if (_cache is not null && DateTime.UtcNow < _cacheExpiry)
            return _cache;

        var items = await TryFetchJsonAsync(ct).ConfigureAwait(false)
                 ?? await TryParseChangelogAsync(ct).ConfigureAwait(false)
                 ?? [];

        _cache       = items;
        _cacheExpiry = DateTime.UtcNow.AddMinutes(30);
        return items;
    }

    // -- JSON feed (primary) -----------------------------------------------

    private static async Task<IReadOnlyList<WelcomeNewsItem>?> TryFetchJsonAsync(CancellationToken ct)
    {
        try
        {
            var json  = await Http.GetStringAsync(NewsFeedUrl, ct).ConfigureAwait(false);
            var dtos  = JsonSerializer.Deserialize<List<NewsFeedDto>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (dtos is null || dtos.Count == 0) return null;

            return dtos
                .Where(d => !string.IsNullOrWhiteSpace(d.Title))
                .Select(d => new WelcomeNewsItem(
                    Title:    d.Title!,
                    Summary:  d.Summary ?? string.Empty,
                    Category: d.Category ?? "Feature",
                    Date:     DateTime.TryParse(d.Date, out var dt) ? dt : DateTime.UtcNow,
                    Url:      d.Url))
                .ToList();
        }
        catch { return null; }
    }

    // -- Changelog fallback (secondary) ------------------------------------

    private static readonly Regex VersionHeader =
        new(@"^##\s+\[([^\]]+)\](?:\s+[—-]+\s+(\S+))?(?:\s+[—-]+\s+(.+))?$",
            RegexOptions.Compiled);

    private static readonly Regex SectionHeader =
        new(@"^###\s+(.+)$", RegexOptions.Compiled);

    private static readonly Regex EntryLine =
        new(@"^-\s+(.+)$", RegexOptions.Compiled);

    private static async Task<IReadOnlyList<WelcomeNewsItem>?> TryParseChangelogAsync(CancellationToken ct)
    {
        try
        {
            var content = await Http.GetStringAsync(ChangelogUrl, ct).ConfigureAwait(false);
            return ParseChangelogToNews(content.Split('\n'));
        }
        catch { return null; }
    }

    private static List<WelcomeNewsItem> ParseChangelogToNews(string[] lines)
    {
        var result       = new List<WelcomeNewsItem>();
        string?  version = null;
        DateTime date    = DateTime.UtcNow;
        string   section = "Feature";
        int      vCount  = 0;

        foreach (var raw in lines)
        {
            var line = raw.TrimEnd();

            var vm = VersionHeader.Match(line);
            if (vm.Success)
            {
                if (vCount >= 3) break;
                version = vm.Groups[1].Value;
                date    = DateTime.TryParse(vm.Groups[2].Value, out var d) ? d : DateTime.UtcNow;
                section = "Feature";
                vCount++;
                continue;
            }

            if (version is null) continue;

            var sm = SectionHeader.Match(line);
            if (sm.Success)
            {
                section = ResolveCategory(sm.Groups[1].Value);
                continue;
            }

            var em = EntryLine.Match(line);
            if (!em.Success) continue;

            var text = Regex.Replace(em.Groups[1].Value, @"\*\*(.+?)\*\*", "$1").Trim();
            if (string.IsNullOrWhiteSpace(text)) continue;

            // Trim to a reasonable summary length
            var summary = text.Length > 120 ? text[..117] + "…" : text;

            result.Add(new WelcomeNewsItem(
                Title:    $"[{version}] {summary}",
                Summary:  string.Empty,
                Category: section,
                Date:     date,
                Url:      null));
        }

        return result;
    }

    private static string ResolveCategory(string sectionTitle)
    {
        var t = sectionTitle.ToUpperInvariant();
        if (t.Contains("ADDED")   || t.Contains("✨") || t.Contains("FEAT")) return "Feature";
        if (t.Contains("FIXED")   || t.Contains("🐛") || t.Contains("FIX"))  return "Fix";
        if (t.Contains("CHANGED") || t.Contains("🔧") || t.Contains("PERF")) return "Perf";
        if (t.Contains("REMOVED") || t.Contains("🗑") || t.Contains("BREAK")) return "Breaking";
        return "Feature";
    }

    // -- DTO ---------------------------------------------------------------

    private sealed class NewsFeedDto
    {
        public string? Title    { get; set; }
        public string? Summary  { get; set; }
        public string? Category { get; set; }
        public string? Date     { get; set; }
        public string? Url      { get; set; }
    }
}
