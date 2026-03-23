// ==========================================================
// Project: WpfHexEditor.PluginDev
// File: Services/LocalPluginMarketplaceService.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Description:
//     Local-cache implementation of IPluginMarketplaceService.
//     Reads package listings from
//       %APPDATA%\WpfHexEditor\marketplace-cache.json
//     Returns an empty list when the file is absent or invalid.
//     Install copies a .whxplugin into the IDE plugin directory.
//
// Architecture Notes:
//     Pattern: Repository (wraps a JSON file as a data source).
//     No HTTP calls — the cache is populated externally (marketplace sync job).
//     Install is a file-copy operation; the IDE plugin host picks it up on restart
//     or via the IIDEEventBus PluginInstalled notification.
// ==========================================================

using System.IO;
using System.Text.Json;

namespace WpfHexEditor.PluginDev.Services;

/// <summary>
/// Reads the marketplace package listing from a local JSON cache file.
/// Returns empty results when the cache is unavailable.
/// </summary>
public sealed class LocalPluginMarketplaceService : IPluginMarketplaceService
{
    // -----------------------------------------------------------------------
    // Constants
    // -----------------------------------------------------------------------

    private static readonly string CachePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WpfHexEditor", "marketplace-cache.json");

    private static readonly string InstallDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WpfHexEditor", "Plugins");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    // -----------------------------------------------------------------------
    // IPluginMarketplaceService
    // -----------------------------------------------------------------------

    public async Task<IReadOnlyList<MarketplacePackage>> GetFeaturedAsync(CancellationToken ct = default)
    {
        var all = await LoadCacheAsync(ct);
        // Featured = top 6 by rating, then by download count.
        return [..all
            .OrderByDescending(p => p.StarRating)
            .ThenByDescending(p => p.DownloadCount)
            .Take(6)];
    }

    public async Task<IReadOnlyList<MarketplacePackage>> SearchAsync(
        string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return await GetFeaturedAsync(ct);

        var all = await LoadCacheAsync(ct);
        var q   = query.Trim();

        return [..all.Where(p =>
            p.Name.Contains(q,        StringComparison.OrdinalIgnoreCase) ||
            p.Description.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            p.Author.Contains(q,      StringComparison.OrdinalIgnoreCase))];
    }

    public async Task<bool> InstallAsync(
        MarketplacePackage pkg,
        IProgress<double>  progress,
        CancellationToken  ct = default)
    {
        // In the local cache model, the package must already exist as a .whxplugin
        // file somewhere reachable. We simulate a download by copying from a local
        // staging directory if available.
        var stagingPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WpfHexEditor", "marketplace-staging",
            $"{pkg.Id}_{pkg.Version}.whxplugin");

        if (!File.Exists(stagingPath))
        {
            // No staged file — nothing to install.
            return false;
        }

        ct.ThrowIfCancellationRequested();
        Directory.CreateDirectory(InstallDir);

        progress.Report(0.1);

        var destPath = Path.Combine(InstallDir, Path.GetFileName(stagingPath));
        await CopyFileAsync(stagingPath, destPath, ct);

        progress.Report(1.0);
        return true;
    }

    public Task<bool> UninstallAsync(string pluginId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
            return Task.FromResult(false);

        // Find any .whxplugin whose name starts with the plugin ID.
        if (!Directory.Exists(InstallDir))
            return Task.FromResult(false);

        var matches = Directory.EnumerateFiles(InstallDir, "*.whxplugin")
            .Where(f => Path.GetFileNameWithoutExtension(f)
                .StartsWith(pluginId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 0)
            return Task.FromResult(false);

        foreach (var file in matches)
        {
            try { File.Delete(file); }
            catch { /* best effort */ }
        }

        return Task.FromResult(true);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static async Task<IReadOnlyList<MarketplacePackage>> LoadCacheAsync(CancellationToken ct)
    {
        if (!File.Exists(CachePath))
            return [];

        try
        {
            var json = await File.ReadAllTextAsync(CachePath, ct);
            var list = JsonSerializer.Deserialize<List<MarketplacePackage>>(json, JsonOptions);
            return list ?? [];
        }
        catch
        {
            // Corrupt or incompatible cache — return empty.
            return [];
        }
    }

    private static async Task CopyFileAsync(string source, string dest, CancellationToken ct)
    {
        await using var src = File.OpenRead(source);
        await using var dst = File.Create(dest);
        await src.CopyToAsync(dst, 81920, ct);
    }
}
