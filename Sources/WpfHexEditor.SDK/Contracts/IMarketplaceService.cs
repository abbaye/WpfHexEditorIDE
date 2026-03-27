//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using WpfHexEditor.SDK.Models;

namespace WpfHexEditor.SDK.Contracts;

/// <summary>
/// Provides access to the WpfHexEditor plugin marketplace.
/// </summary>
/// <remarks>
/// This interface is a preview stub. The API surface will change when the marketplace
/// backend is implemented. Do not depend on the current method signatures.
/// </remarks>
[Obsolete("Preview API — method signatures will change in SDK 3.0. Do not depend on current shape.")]
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
