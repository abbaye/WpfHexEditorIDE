// ==========================================================
// Project: WpfHexEditor.PluginHost
// File: Services/MarketplaceServiceImpl.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-15
// Description:
//     Implements IMarketplaceService.
//     Phase 7: Local-first marketplace — scans a configurable feed JSON file
//     (local path or http URL). Downloads .whxplugin packages to a temp dir
//     and delegates installation to WpfPluginHost.InstallFromFileAsync.
//
// Architecture Notes:
//     - Pattern: Service + Repository (listings from JSON feed)
//     - Feed format: array of MarketplaceListing JSON objects
//     - Local feed path: %AppData%/WpfHexEditor/marketplace-feed.json
//     - Remote feed: configurable via FeedUrl property (null = local-only)
//     - Download: HttpClient with progress, written to temp file
// ==========================================================

using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Models;

namespace WpfHexEditor.PluginHost.Services;

/// <summary>
/// Local-first marketplace service. Reads a JSON feed of <see cref="MarketplaceListing"/>
/// and downloads .whxplugin packages for installation.
/// </summary>
public sealed class MarketplaceServiceImpl : IMarketplaceService, IDisposable
{
    private static readonly string LocalFeedPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WpfHexEditor", "marketplace-feed.json");

    private static readonly string DownloadCacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WpfHexEditor", "PluginDownloadCache");

    private readonly HttpClient _httpClient;
    private readonly Action<string> _log;

    // Optional remote feed URL (null = local-only)
    public string? FeedUrl { get; set; }

    // ── Progress events ───────────────────────────────────────────────────────
    public event EventHandler<DownloadProgressArgs>? DownloadProgressChanged;

    // ─────────────────────────────────────────────────────────────────────────
    public MarketplaceServiceImpl(Action<string>? logger = null)
    {
        _log = logger ?? (_ => { });
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "WpfHexEditor/1.0");

        Directory.CreateDirectory(DownloadCacheDir);
    }

    // ── IMarketplaceService ───────────────────────────────────────────────────

    /// <summary>
    /// Loads the marketplace feed (local or remote) and returns listings
    /// matching <paramref name="query"/> in name, description, tags, or publisher.
    /// An empty query returns all listings.
    /// </summary>
    public async Task<IReadOnlyList<MarketplaceListing>> SearchAsync(
        string query, CancellationToken ct = default)
    {
        var listings = await LoadFeedAsync(ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(query)) return listings;

        var q = query.Trim().ToUpperInvariant();
        return listings.Where(l =>
            l.Name.ToUpperInvariant().Contains(q)
            || l.Description.ToUpperInvariant().Contains(q)
            || l.Publisher.ToUpperInvariant().Contains(q)
            || l.Tags.Any(t => t.ToUpperInvariant().Contains(q))
            || l.Category.ToUpperInvariant().Contains(q))
            .ToList();
    }

    /// <summary>
    /// Downloads the .whxplugin package for <paramref name="listingId"/>.
    /// Returns the local file path of the cached package.
    /// </summary>
    public async Task<string> DownloadAsync(string listingId, CancellationToken ct = default)
    {
        var listings = await LoadFeedAsync(ct).ConfigureAwait(false);
        var listing = listings.FirstOrDefault(l => l.ListingId == listingId)
            ?? throw new KeyNotFoundException($"Marketplace listing '{listingId}' not found.");

        if (string.IsNullOrEmpty(listing.DownloadUrl))
            throw new InvalidOperationException($"Listing '{listingId}' has no download URL.");

        var fileName = $"{listingId}_{listing.Version}.whxplugin";
        var targetPath = Path.Combine(DownloadCacheDir, fileName);

        // Reuse cached download if already present
        if (File.Exists(targetPath))
        {
            _log($"[Marketplace] Using cached package: {targetPath}");
            return targetPath;
        }

        _log($"[Marketplace] Downloading '{listing.Name}' from {listing.DownloadUrl}");

        using var response = await _httpClient
            .GetAsync(listing.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
        var tmpPath = targetPath + ".tmp";

        await using (var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
        await using (var file = File.Create(tmpPath))
        {
            var buffer = new byte[81920];
            long received = 0;
            int bytesRead;

            while ((bytesRead = await stream.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
            {
                await file.WriteAsync(buffer.AsMemory(0, bytesRead), ct).ConfigureAwait(false);
                received += bytesRead;

                if (totalBytes > 0)
                    DownloadProgressChanged?.Invoke(this, new DownloadProgressArgs(
                        listingId, listing.Name, received, totalBytes));
            }
        }

        File.Move(tmpPath, targetPath, overwrite: true);
        _log($"[Marketplace] Downloaded to: {targetPath}");
        return targetPath;
    }

    // ── Feed loading ──────────────────────────────────────────────────────────

    private async Task<IReadOnlyList<MarketplaceListing>> LoadFeedAsync(CancellationToken ct)
    {
        // Try remote feed first if configured
        if (!string.IsNullOrEmpty(FeedUrl))
        {
            try
            {
                var remote = await _httpClient
                    .GetFromJsonAsync<List<MarketplaceListing>>(FeedUrl, ct)
                    .ConfigureAwait(false);

                if (remote is { Count: > 0 })
                {
                    // Cache locally for offline use
                    await SaveLocalFeedAsync(remote, ct).ConfigureAwait(false);
                    return remote;
                }
            }
            catch (Exception ex)
            {
                _log($"[Marketplace] Remote feed unavailable ({ex.Message}), falling back to local.");
            }
        }

        // Local feed
        if (!File.Exists(LocalFeedPath))
        {
            _log("[Marketplace] No local feed found. Returning empty listing.");
            return [];
        }

        try
        {
            var json = await File.ReadAllTextAsync(LocalFeedPath, ct).ConfigureAwait(false);
            return JsonSerializer.Deserialize<List<MarketplaceListing>>(json) ?? [];
        }
        catch (Exception ex)
        {
            _log($"[Marketplace] Failed to read local feed: {ex.Message}");
            return [];
        }
    }

    private async Task SaveLocalFeedAsync(IReadOnlyList<MarketplaceListing> listings, CancellationToken ct)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LocalFeedPath)!);
            var json = JsonSerializer.Serialize(listings, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(LocalFeedPath, json, ct).ConfigureAwait(false);
        }
        catch { /* best-effort cache */ }
    }

    public void Dispose() => _httpClient.Dispose();
}

/// <summary>Progress args for marketplace package downloads.</summary>
public sealed class DownloadProgressArgs : EventArgs
{
    public string ListingId { get; }
    public string Name { get; }
    public long BytesReceived { get; }
    public long TotalBytes { get; }
    public double Percent => TotalBytes > 0 ? BytesReceived * 100.0 / TotalBytes : 0;

    public DownloadProgressArgs(string listingId, string name, long received, long total)
    {
        ListingId     = listingId;
        Name          = name;
        BytesReceived = received;
        TotalBytes    = total;
    }
}
