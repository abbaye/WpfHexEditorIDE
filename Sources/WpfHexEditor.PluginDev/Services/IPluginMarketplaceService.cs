// ==========================================================
// Project: WpfHexEditor.PluginDev
// File: Services/IPluginMarketplaceService.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Description:
//     Service contract for the in-IDE plugin marketplace.
//     Provides search, install, and uninstall operations.
//     The default implementation reads from a local cache file.
//
// Architecture Notes:
//     Pattern: Service Abstraction — interface-first design enables
//     swapping the local cache for an HTTP-backed implementation later.
// ==========================================================

namespace WpfHexEditor.PluginDev.Services;

// -----------------------------------------------------------------------
// Data model
// -----------------------------------------------------------------------

/// <summary>
/// Represents a plugin package listed in the marketplace.
/// </summary>
public sealed class MarketplacePackage
{
    public required string Id            { get; init; }
    public required string Name          { get; init; }
    public required string Author        { get; init; }
    public required string Version       { get; init; }
    public required string Description   { get; init; }
    public          int    DownloadCount { get; init; }
    public          double StarRating    { get; init; }
}

// -----------------------------------------------------------------------
// Service interface
// -----------------------------------------------------------------------

/// <summary>
/// Plugin marketplace service — browse, install, and manage plugins.
/// </summary>
public interface IPluginMarketplaceService
{
    /// <summary>Returns featured/promoted packages from the marketplace.</summary>
    Task<IReadOnlyList<MarketplacePackage>> GetFeaturedAsync(CancellationToken ct = default);

    /// <summary>Searches packages by name, description, or author.</summary>
    Task<IReadOnlyList<MarketplacePackage>> SearchAsync(string query, CancellationToken ct = default);

    /// <summary>
    /// Downloads and installs <paramref name="pkg"/> into the IDE plugin directory.
    /// </summary>
    /// <param name="pkg">Package to install.</param>
    /// <param name="progress">Progress callback (0.0–1.0).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> on success.</returns>
    Task<bool> InstallAsync(
        MarketplacePackage  pkg,
        IProgress<double>   progress,
        CancellationToken   ct = default);

    /// <summary>
    /// Uninstalls the plugin identified by <paramref name="pluginId"/>.
    /// </summary>
    Task<bool> UninstallAsync(string pluginId, CancellationToken ct = default);
}
