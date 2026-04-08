// ==========================================================
// Project: WpfHexEditor.PluginHost
// File: Services/MarketplaceServiceImpl.cs
// Description:
//     IMarketplaceService v2 — GitHub Releases API + local JSON feed fallback.
//     Install pipeline: download → SHA256 verify → extract to PluginsDir.
//     Rate-limit protection: 1h GitHub cache + optional PAT from AppSettings.
// Architecture Notes:
//     Pattern: Service + Repository + Strategy (GitHub vs local feed).
//     Thread-safe: all mutable state behind _lock.
// ==========================================================

using System.IO;
using System.Net.Http;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Models;

namespace WpfHexEditor.PluginHost.Services;

/// <summary>
/// v2 marketplace service backed by GitHub Releases API with SHA-256 integrity verification.
/// Falls back to a local JSON feed cache when GitHub is rate-limited or unavailable.
/// </summary>
public sealed class MarketplaceServiceImpl : IMarketplaceService, IDisposable
{
    // ── Paths ─────────────────────────────────────────────────────────────────

    private static string LocalFeedPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WpfHexEditor", "marketplace-feed.json");

    private static string DownloadCacheDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WpfHexEditor", "PluginDownloadCache");

    private readonly string? _pluginsBaseDirOverride;
    private string PluginsDir => _pluginsBaseDirOverride is not null
        ? Path.Combine(_pluginsBaseDirOverride, "Plugins")
        : Path.Combine(AppContext.BaseDirectory, "Plugins");

    // ── Infrastructure ────────────────────────────────────────────────────────

    private readonly HttpClient              _http;
    private readonly GitHubReleasesClient    _ghClient;
    private readonly Action<string>          _log;
    private readonly Dictionary<string, MarketplaceListing> _installed  = new(StringComparer.OrdinalIgnoreCase);
    private readonly object                  _lock = new();

    // ── Official registry: GitHub repos → listingId prefix ───────────────────

    private static readonly (string Owner, string Repo)[] OfficialRepos =
    [
        ("abbaye", "WpfHexEditorControl")
    ];

    // ── Constructor ───────────────────────────────────────────────────────────

    /// <summary>Creates a production instance backed by <c>AppContext.BaseDirectory/Plugins</c>.</summary>
    public MarketplaceServiceImpl(string? gitHubToken = null, Action<string>? logger = null)
        : this(pluginsBaseDir: null, gitHubToken, logger) { }

    /// <summary>
    /// Creates a test-isolation instance with a custom base directory.
    /// Only for use in unit tests — allows injecting a temp dir for PluginsDir.
    /// </summary>
    internal MarketplaceServiceImpl(string? pluginsBaseDir, string? gitHubToken = null, Action<string>? logger = null)
    {
        _pluginsBaseDirOverride = pluginsBaseDir;
        _log      = logger ?? (_ => { });
        _http     = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        _ghClient = new GitHubReleasesClient(gitHubToken);

        Directory.CreateDirectory(DownloadCacheDir);
        Directory.CreateDirectory(PluginsDir);
        ScanInstalledPlugins();
    }

    // ── IMarketplaceService ───────────────────────────────────────────────────

    public event EventHandler<InstallProgressEventArgs>? InstallProgressChanged;

    public async Task<IReadOnlyList<MarketplaceListing>> SearchAsync(
        string query,
        MarketplaceFilter? filter = null,
        CancellationToken ct = default)
    {
        var all = await LoadAllListingsAsync(ct);
        ApplyInstallState(all);

        IEnumerable<MarketplaceListing> results = all;

        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim().ToUpperInvariant();
            results = results.Where(l =>
                l.Name.ToUpperInvariant().Contains(q)
                || l.Description.ToUpperInvariant().Contains(q)
                || l.Publisher.ToUpperInvariant().Contains(q)
                || l.Tags.Any(t => t.ToUpperInvariant().Contains(q)));
        }

        if (filter is not null)
        {
            if (!string.IsNullOrEmpty(filter.Category))
                results = results.Where(l => l.Category.Equals(filter.Category, StringComparison.OrdinalIgnoreCase));
            if (filter.VerifiedOnly)
                results = results.Where(l => l.Verified);
            if (!string.IsNullOrEmpty(filter.MinVersion)
                && System.Version.TryParse(filter.MinVersion, out var minV))
                results = results.Where(l => System.Version.TryParse(l.Version, out var lv) && lv >= minV);
        }

        return results.ToList();
    }

    public async Task<MarketplaceListing?> GetByIdAsync(string listingId, CancellationToken ct = default)
    {
        var all = await LoadAllListingsAsync(ct);
        var found = all.FirstOrDefault(l => l.ListingId.Equals(listingId, StringComparison.OrdinalIgnoreCase));
        if (found is not null) ApplyInstallState([found]);
        return found;
    }

    public async Task<InstallResult> InstallAsync(
        string listingId,
        IProgress<InstallProgress>? progress = null,
        CancellationToken ct = default)
    {
        var listing = await GetByIdAsync(listingId, ct);
        if (listing is null)
            return new InstallResult(false, $"Listing '{listingId}' not found.", null);

        if (string.IsNullOrEmpty(listing.DownloadUrl))
            return new InstallResult(false, $"Listing '{listingId}' has no download URL.", null);

        var fileName = $"{listingId}.whxplugin";
        var tmpPath  = Path.Combine(DownloadCacheDir, fileName + ".tmp");
        var cachePath= Path.Combine(DownloadCacheDir, fileName);

        try
        {
            // Download with progress
            ReportProgress(progress, listingId, 0, "Downloading…");
            await DownloadWithProgressAsync(listing.DownloadUrl, tmpPath, listingId, listing.Name, progress, ct);
            File.Move(tmpPath, cachePath, overwrite: true);

            // SHA256 verify
            ReportProgress(progress, listingId, 85, "Verifying integrity…");
            if (!string.IsNullOrEmpty(listing.Sha256))
            {
                var ok = await PluginIntegrityService.VerifyAsync(cachePath, listing.Sha256, ct);
                if (!ok)
                {
                    File.Delete(cachePath);
                    return new InstallResult(false,
                        $"SHA-256 mismatch for '{listing.Name}'. Package rejected.", null);
                }
            }

            // Back up existing version to .bak/ before overwriting
            ReportProgress(progress, listingId, 85, "Backing up…");
            var destDir = Path.Combine(PluginsDir, listingId);
            var bakDir  = Path.Combine(destDir, ".bak");
            if (Directory.Exists(destDir))
            {
                try
                {
                    if (Directory.Exists(bakDir)) Directory.Delete(bakDir, recursive: true);
                    Directory.CreateDirectory(bakDir);
                    foreach (var file in Directory.EnumerateFiles(destDir, "*", SearchOption.TopDirectoryOnly))
                        File.Copy(file, Path.Combine(bakDir, Path.GetFileName(file)), overwrite: true);
                }
                catch { /* backup is best-effort */ }
            }

            // Extract to PluginsDir/<ListingId>/
            ReportProgress(progress, listingId, 90, "Installing…");
            Directory.CreateDirectory(destDir);
            System.IO.Compression.ZipFile.ExtractToDirectory(cachePath, destDir, overwriteFiles: true);

            lock (_lock)
            {
                listing.InstalledVersion = listing.Version;
                listing.IsVerified = true;
                _installed[listingId] = listing;
            }

            ReportProgress(progress, listingId, 100, "Installed.");
            _log($"[Marketplace] Installed '{listing.Name}' v{listing.Version} → {destDir}");
            return new InstallResult(true, null, destDir);
        }
        catch (Exception ex)
        {
            if (File.Exists(tmpPath)) try { File.Delete(tmpPath); } catch { }
            _log($"[Marketplace] Install failed for '{listingId}': {ex.Message}");
            return new InstallResult(false, ex.Message, null);
        }
    }

    public Task<bool> UninstallAsync(string listingId, CancellationToken ct = default)
    {
        var destDir = Path.Combine(PluginsDir, listingId);
        try
        {
            if (Directory.Exists(destDir)) Directory.Delete(destDir, recursive: true);
            lock (_lock) _installed.Remove(listingId);
            _log($"[Marketplace] Uninstalled '{listingId}'.");
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _log($"[Marketplace] Uninstall failed for '{listingId}': {ex.Message}");
            return Task.FromResult(false);
        }
    }

    public Task<bool> RollbackAsync(string listingId, CancellationToken ct = default)
    {
        var destDir = Path.Combine(PluginsDir, listingId);
        var bakDir  = Path.Combine(destDir, ".bak");

        if (!Directory.Exists(bakDir))
        {
            _log($"[Marketplace] Rollback failed for '{listingId}': no .bak/ directory found.");
            return Task.FromResult(false);
        }

        try
        {
            // Restore .bak/ files over the current install
            foreach (var file in Directory.EnumerateFiles(bakDir, "*", SearchOption.TopDirectoryOnly))
                File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), overwrite: true);

            _log($"[Marketplace] Rolled back '{listingId}' from .bak/.");
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _log($"[Marketplace] Rollback failed for '{listingId}': {ex.Message}");
            return Task.FromResult(false);
        }
    }

    public async Task<string?> GetChangelogAsync(string listingId, CancellationToken ct = default)
    {
        // Try GitHub releases body for each official repo owner.
        foreach (var (owner, repo) in OfficialRepos)
        {
            try
            {
                var releases = await _ghClient.GetReleasesAsync(owner, repo, ct);
                var match    = releases.FirstOrDefault(r => r.ListingId == listingId);
                if (match is not null)
                {
                    // GitHubReleasesClient stores the release body in Description
                    return match.Description;
                }
            }
            catch { /* continue */ }
        }
        return null;
    }

    public Task<IReadOnlyList<MarketplaceListing>> GetInstalledAsync(CancellationToken ct = default)
    {
        lock (_lock) { return Task.FromResult<IReadOnlyList<MarketplaceListing>>(_installed.Values.ToList()); }
    }

    public async Task<IReadOnlyList<MarketplaceListing>> GetUpdatesAsync(CancellationToken ct = default)
    {
        var all = await LoadAllListingsAsync(ct);
        ApplyInstallState(all);
        return all.Where(l => l.HasUpdate).ToList();
    }

    public bool IsInstalled(string listingId)
    {
        lock (_lock) { return _installed.ContainsKey(listingId); }
    }

    public async Task<bool> VerifyIntegrityAsync(string listingId, CancellationToken ct = default)
    {
        var listing = await GetByIdAsync(listingId, ct);
        if (listing is null || string.IsNullOrEmpty(listing.Sha256)) return false;

        var dllPath = Path.Combine(PluginsDir, listingId, $"{listingId}.dll");
        if (!File.Exists(dllPath)) return false;

        return await PluginIntegrityService.VerifyAsync(dllPath, listing.Sha256, ct);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<IReadOnlyList<MarketplaceListing>> LoadAllListingsAsync(CancellationToken ct)
    {
        var result = new List<MarketplaceListing>();

        // Fetch from GitHub official repos
        foreach (var (owner, repo) in OfficialRepos)
        {
            try
            {
                var ghListings = await _ghClient.GetReleasesAsync(owner, repo, ct);
                result.AddRange(ghListings);
            }
            catch { /* fallthrough to local cache */ }
        }

        if (result.Count > 0)
        {
            // Persist as local cache for offline use
            _ = SaveLocalFeedAsync(result, ct);
            return result;
        }

        // Fallback: local JSON cache
        return await LoadLocalFeedAsync(ct);
    }

    private async Task<IReadOnlyList<MarketplaceListing>> LoadLocalFeedAsync(CancellationToken ct)
    {
        if (!File.Exists(LocalFeedPath)) return [];
        try
        {
            var json = await File.ReadAllTextAsync(LocalFeedPath, ct);
            return System.Text.Json.JsonSerializer.Deserialize<List<MarketplaceListing>>(json) ?? [];
        }
        catch { return []; }
    }

    private async Task SaveLocalFeedAsync(IReadOnlyList<MarketplaceListing> listings, CancellationToken ct)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LocalFeedPath)!);
            var json = System.Text.Json.JsonSerializer.Serialize(
                listings, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(LocalFeedPath, json, ct);
        }
        catch { /* best-effort */ }
    }

    private void ApplyInstallState(IReadOnlyList<MarketplaceListing> listings)
    {
        lock (_lock)
        {
            foreach (var l in listings)
            {
                if (_installed.TryGetValue(l.ListingId, out var inst))
                {
                    l.InstalledVersion = inst.InstalledVersion;
                    l.IsVerified       = inst.IsVerified;
                }
            }
        }
    }

    private void ScanInstalledPlugins()
    {
        if (!Directory.Exists(PluginsDir)) return;
        foreach (var dir in Directory.EnumerateDirectories(PluginsDir))
        {
            var name = Path.GetFileName(dir);
            if (string.IsNullOrEmpty(name)) continue;
            lock (_lock)
            {
                _installed[name] = new MarketplaceListing
                {
                    ListingId        = name,
                    Name             = name,
                    InstalledVersion = "0.0.0"  // version read at runtime
                };
            }
        }
    }

    private async Task DownloadWithProgressAsync(
        string url,
        string destPath,
        string listingId,
        string listingName,
        IProgress<InstallProgress>? progress,
        CancellationToken ct)
    {
        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var total  = response.Content.Headers.ContentLength ?? -1L;
        var buffer = new byte[81920];
        long received = 0;

        await using var src  = await response.Content.ReadAsStreamAsync(ct);
        await using var dest = File.Create(destPath);

        int read;
        while ((read = await src.ReadAsync(buffer, ct)) > 0)
        {
            await dest.WriteAsync(buffer.AsMemory(0, read), ct);
            received += read;

            if (total > 0)
            {
                var pct = (int)(received * 80L / total); // 0-80% for download phase
                ReportProgress(progress, listingId, pct, $"Downloading… {pct}%");
            }
        }
    }

    private void ReportProgress(
        IProgress<InstallProgress>? progress,
        string listingId, int pct, string msg)
    {
        var p = new InstallProgress(listingId, pct, msg);
        progress?.Report(p);
        InstallProgressChanged?.Invoke(this, new InstallProgressEventArgs(p));
    }

    public void Dispose()
    {
        _http.Dispose();
        _ghClient.Dispose();
    }
}
