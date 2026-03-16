// ==========================================================
// Project: WpfHexEditor.ProjectSystem
// File: Services/NuGet/NuGetV3Client.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Concrete implementation of INuGetClient using the NuGet V3 REST API.
//     Lazily resolves the service index on first use and caches endpoint URLs.
//     All network failures are caught and logged; callers receive empty lists.
//
// Architecture Notes:
//     Pattern: Singleton (static HttpClient — best practice for HttpClient lifetime)
//     Service index URL: https://api.nuget.org/v3/index.json
//     Search endpoint  : @type = SearchQueryService
//     Registration     : @type = RegistrationsBaseUrl
// ==========================================================

using System.Net.Http;
using System.Text.Json;

namespace WpfHexEditor.ProjectSystem.Services.NuGet;

/// <summary>
/// NuGet V3 REST API client.
/// </summary>
public sealed class NuGetV3Client : INuGetClient
{
    // Static HttpClient — reused across instances (avoids socket exhaustion).
    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(15),
        DefaultRequestHeaders = { { "User-Agent", "WpfHexEditor/1.0" } }
    };

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private const string ServiceIndexUrl = "https://api.nuget.org/v3/index.json";

    // Lazily-resolved endpoint URLs (populated on first call).
    private string? _searchBaseUrl;
    private string? _registrationsBaseUrl;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    // ── Public API ────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<IReadOnlyList<NuGetSearchResult>> SearchAsync(
        string query,
        bool   includePrerelease,
        int    skip,
        int    take,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureEndpointsResolvedAsync(cancellationToken);
            if (_searchBaseUrl is null) return [];

            var url = BuildSearchUrl(query, includePrerelease, skip, take);
            var json = await _http.GetStringAsync(url, cancellationToken);
            var response = JsonSerializer.Deserialize<NuGetSearchResponse>(json, _jsonOptions);
            return response?.Data ?? [];
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine($"[NuGetV3Client] Search failed: {ex.Message}");
            return [];
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> GetVersionsAsync(
        string packageId,
        bool   includePrerelease,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureEndpointsResolvedAsync(cancellationToken);
            if (_registrationsBaseUrl is null) return [];

            var url = $"{_registrationsBaseUrl.TrimEnd('/')}/{packageId.ToLowerInvariant()}/index.json";
            var json = await _http.GetStringAsync(url, cancellationToken);
            var index = JsonSerializer.Deserialize<NuGetRegistrationIndex>(json, _jsonOptions);

            return ExtractVersions(index, includePrerelease);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine($"[NuGetV3Client] GetVersions failed: {ex.Message}");
            return [];
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task EnsureEndpointsResolvedAsync(CancellationToken ct)
    {
        if (_searchBaseUrl is not null) return;

        await _initLock.WaitAsync(ct);
        try
        {
            if (_searchBaseUrl is not null) return;

            var json  = await _http.GetStringAsync(ServiceIndexUrl, ct);
            var index = JsonSerializer.Deserialize<NuGetServiceIndex>(json, _jsonOptions);

            _searchBaseUrl        = ResolveResource(index, "SearchQueryService");
            _registrationsBaseUrl = ResolveResource(index, "RegistrationsBaseUrl");
        }
        finally
        {
            _initLock.Release();
        }
    }

    private static string? ResolveResource(NuGetServiceIndex? index, string typePrefix)
        => index?.Resources
               .FirstOrDefault(r => r.Type.StartsWith(typePrefix, StringComparison.OrdinalIgnoreCase))
               ?.Id;

    private string BuildSearchUrl(string query, bool prerelease, int skip, int take)
    {
        var encoded = Uri.EscapeDataString(query);
        return $"{_searchBaseUrl}?q={encoded}&take={take}&skip={skip}&prerelease={prerelease.ToString().ToLowerInvariant()}";
    }

    private static IReadOnlyList<string> ExtractVersions(NuGetRegistrationIndex? index, bool includePrerelease)
    {
        if (index is null) return [];

        var versions = index.Items
            .SelectMany(page => page.Items ?? [])
            .Select(leaf => leaf.CatalogEntry)
            .Where(e => e.Listed && (includePrerelease || !IsPrerelease(e.Version)))
            .Select(e => e.Version)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(v => v, StringComparer.OrdinalIgnoreCase)
            .Take(100)   // Cap at 100 versions for performance
            .ToList();

        return versions;
    }

    private static bool IsPrerelease(string version)
        => version.Contains('-');
}
