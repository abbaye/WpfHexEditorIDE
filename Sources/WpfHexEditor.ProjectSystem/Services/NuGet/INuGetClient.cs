// ==========================================================
// Project: WpfHexEditor.ProjectSystem
// File: Services/NuGet/INuGetClient.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Contract for querying the NuGet V3 REST API.
//     Implementations must be thread-safe.
//
// Architecture Notes:
//     Pattern: Interface Segregation — only the 3 operations the
//     NuGet Manager UI actually needs are exposed here.
// ==========================================================

namespace WpfHexEditor.ProjectSystem.Services.NuGet;

/// <summary>
/// Provides read-only access to the NuGet V3 package feed.
/// </summary>
public interface INuGetClient
{
    /// <summary>
    /// Searches the feed for packages matching <paramref name="query"/>.
    /// Returns an empty list on network failure (no exceptions propagated to callers).
    /// </summary>
    Task<IReadOnlyList<NuGetSearchResult>> SearchAsync(
        string query,
        bool   includePrerelease,
        int    skip,
        int    take,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all listed version strings for <paramref name="packageId"/>,
    /// sorted descending (newest first).
    /// Returns an empty list on network failure.
    /// </summary>
    Task<IReadOnlyList<string>> GetVersionsAsync(
        string packageId,
        bool   includePrerelease,
        CancellationToken cancellationToken = default);
}
