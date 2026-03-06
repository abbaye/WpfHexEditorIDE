// ==========================================================
// Project: WpfHexEditor.SDK
// File: IMarketplaceService.cs
// Author: Auto
// Created: 2026-03-06
// Description:
//     Stub interface for the future WpfHexEditor marketplace client.
//     Enables the SDK to define the contract without a concrete implementation.
//
// Architecture Notes:
//     The full marketplace client is a separate plan.
//     This stub allows Plugin Manager to call these methods without a real backend.
//
// ==========================================================

using WpfHexEditor.SDK.Models;

namespace WpfHexEditor.SDK.Contracts;

/// <summary>
/// Provides access to the WpfHexEditor plugin marketplace.
/// </summary>
public interface IMarketplaceService
{
    /// <summary>
    /// Searches the marketplace for plugins matching the query.
    /// </summary>
    /// <param name="query">Search text (name, description, tags).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of matching marketplace listings.</returns>
    Task<IReadOnlyList<MarketplaceListing>> SearchAsync(string query, CancellationToken ct = default);

    /// <summary>
    /// Downloads a plugin package (.whxplugin) from the marketplace.
    /// </summary>
    /// <param name="listingId">Marketplace listing identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Local file path of the downloaded .whxplugin package.</returns>
    Task<string> DownloadAsync(string listingId, CancellationToken ct = default);
}
