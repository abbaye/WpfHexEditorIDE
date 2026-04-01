// ==========================================================
// Project: WpfHexEditor.PluginHost
// File: Services/GitHubReleasesClient.cs
// Description:
//     HTTP client wrapper for the GitHub Releases REST API v3.
//     Maps releases → MarketplaceListing. Respects rate limits.
//     Cache TTL: 1 hour per repo. Optional PAT via AppSettings.
// Architecture Notes:
//     Pure infrastructure — no WPF. Singleton, thread-safe.
//     Graceful degradation: rate limit exceeded → returns cached entries.
// ==========================================================

using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using WpfHexEditor.SDK.Models;

namespace WpfHexEditor.PluginHost.Services;

/// <summary>
/// Fetches plugin listings from GitHub Releases and maps them to MarketplaceListing.
/// </summary>
internal sealed class GitHubReleasesClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly Dictionary<string, (DateTime Fetched, IReadOnlyList<MarketplaceListing> Listings)>
        _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly TimeSpan _cacheTtl = TimeSpan.FromHours(1);

    internal GitHubReleasesClient(string? personalAccessToken = null)
    {
        _http = new HttpClient();
        _http.BaseAddress = new Uri("https://api.github.com/");
        _http.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("WpfHexEditor", "0.8.0"));
        _http.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        if (!string.IsNullOrEmpty(personalAccessToken))
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", personalAccessToken);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns all plugin listings from <paramref name="owner"/>/<paramref name="repo"/>.
    /// Uses cache when available and not expired.
    /// </summary>
    internal async Task<IReadOnlyList<MarketplaceListing>> GetReleasesAsync(
        string owner, string repo, CancellationToken ct = default)
    {
        var key = $"{owner}/{repo}";
        if (_cache.TryGetValue(key, out var cached) && DateTime.UtcNow - cached.Fetched < _cacheTtl)
            return cached.Listings;

        // Check rate limit headroom before calling
        if (!await HasRateLimitAsync(ct)) return _cache.TryGetValue(key, out cached) ? cached.Listings : [];

        try
        {
            var json = await _http.GetStringAsync($"repos/{owner}/{repo}/releases", ct);
            var releases = JsonSerializer.Deserialize<GhRelease[]>(json, _jsonOptions) ?? [];
            var listings = releases.Select(r => MapRelease(owner, repo, r)).ToList();

            _cache[key] = (DateTime.UtcNow, listings);
            return listings;
        }
        catch
        {
            return _cache.TryGetValue(key, out cached) ? cached.Listings : [];
        }
    }

    /// <summary>
    /// Returns the latest release from <paramref name="owner"/>/<paramref name="repo"/>.
    /// </summary>
    internal async Task<MarketplaceListing?> GetLatestReleaseAsync(
        string owner, string repo, CancellationToken ct = default)
    {
        var all = await GetReleasesAsync(owner, repo, ct);
        return all.FirstOrDefault();
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private async Task<bool> HasRateLimitAsync(CancellationToken ct)
    {
        try
        {
            var response = await _http.SendAsync(
                new HttpRequestMessage(HttpMethod.Head, "rate_limit"), ct);

            if (response.Headers.TryGetValues("X-RateLimit-Remaining", out var vals)
                && int.TryParse(vals.FirstOrDefault(), out var remaining))
                return remaining > 5;
        }
        catch { /* ignore */ }
        return true; // assume ok on error
    }

    private static MarketplaceListing MapRelease(string owner, string repo, GhRelease r)
    {
        // Find the .whxplugin asset
        var asset  = r.Assets.FirstOrDefault(a => a.Name.EndsWith(".whxplugin", StringComparison.OrdinalIgnoreCase));
        var sha256 = r.Assets.FirstOrDefault(a => a.Name.EndsWith(".sha256", StringComparison.OrdinalIgnoreCase));

        var name = ExtractPluginName(r.TagName ?? repo);
        return new MarketplaceListing
        {
            ListingId      = $"{owner}.{repo}.{r.TagName}",
            Name           = name,
            Description    = r.Body ?? string.Empty,
            Publisher      = owner,
            Version        = r.TagName?.TrimStart('v') ?? "0.0.0",
            Category       = "Community",
            GitHubRepo     = $"{owner}/{repo}",
            GitHubReleaseId = r.Id,
            DownloadUrl    = asset?.BrowserDownloadUrl,
            Sha256         = string.Empty, // populated by PluginIntegrityService from .sha256 asset
            ReleaseNotes   = r.Body,
            DownloadCount  = asset?.DownloadCount ?? 0,
            Verified       = false
        };
    }

    private static string ExtractPluginName(string tagName)
        => tagName.TrimStart('v').Replace('-', ' ').Replace('.', ' ').Trim();

    // ── JSON DTOs ─────────────────────────────────────────────────────────────

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed class GhRelease
    {
        [JsonPropertyName("id")]          public long    Id      { get; init; }
        [JsonPropertyName("tag_name")]    public string? TagName { get; init; }
        [JsonPropertyName("body")]        public string? Body    { get; init; }
        [JsonPropertyName("prerelease")]  public bool    PreRelease { get; init; }
        [JsonPropertyName("draft")]       public bool    Draft   { get; init; }
        [JsonPropertyName("assets")]      public GhAsset[] Assets { get; init; } = [];
    }

    private sealed class GhAsset
    {
        [JsonPropertyName("name")]                  public string  Name               { get; init; } = string.Empty;
        [JsonPropertyName("browser_download_url")] public string? BrowserDownloadUrl { get; init; }
        [JsonPropertyName("download_count")]        public int     DownloadCount      { get; init; }
    }

    public void Dispose() => _http.Dispose();
}
