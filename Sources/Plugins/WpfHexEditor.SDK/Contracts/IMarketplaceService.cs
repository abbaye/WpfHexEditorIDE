// ==========================================================
// Project: WpfHexEditor.SDK
// File: Contracts/IMarketplaceService.cs
// Description:
//     v2 marketplace contract — GitHub Releases API backed, SHA256 verified.
//     Replaces the v1 Obsolete preview stub (SDK 3.0 promotion).
// Architecture Notes:
//     Implemented by WpfHexEditor.PluginHost.Services.MarketplaceServiceImpl.
//     All async operations support CancellationToken + IProgress<InstallProgress>.
// ==========================================================

using WpfHexEditor.SDK.Models;

namespace WpfHexEditor.SDK.Contracts;

// ── Supporting types ──────────────────────────────────────────────────────────

/// <summary>Filters applied to marketplace search results.</summary>
public sealed record MarketplaceFilter(
    string? Category    = null,
    bool    VerifiedOnly = false,
    string? MinVersion  = null);

/// <summary>Result of an install operation.</summary>
public sealed record InstallResult(
    bool    Success,
    string? ErrorMessage,
    string? InstalledPath);

/// <summary>Progress snapshot during download/install.</summary>
public sealed record InstallProgress(
    string ListingId,
    int    PercentComplete,
    string StatusMessage);

/// <summary>Event args for install progress notifications.</summary>
public sealed class InstallProgressEventArgs : EventArgs
{
    public InstallProgress Progress { get; }
    public InstallProgressEventArgs(InstallProgress p) => Progress = p;
}

// ── Contract ──────────────────────────────────────────────────────────────────

/// <summary>
/// WpfHexEditor plugin marketplace facade.
/// v2: GitHub Releases API + SHA256 integrity verification.
/// </summary>
public interface IMarketplaceService
{
    // ── Browsing ──────────────────────────────────────────────────────────────

    /// <summary>Searches the marketplace for plugins matching <paramref name="query"/>.</summary>
    Task<IReadOnlyList<MarketplaceListing>> SearchAsync(
        string query,
        MarketplaceFilter? filter = null,
        CancellationToken ct = default);

    /// <summary>Returns the listing with the given <paramref name="listingId"/>, or null.</summary>
    Task<MarketplaceListing?> GetByIdAsync(string listingId, CancellationToken ct = default);

    // ── Installation ──────────────────────────────────────────────────────────

    /// <summary>
    /// Downloads, verifies SHA256, and installs the plugin identified by <paramref name="listingId"/>.
    /// Reports progress via <paramref name="progress"/>. SHA256 mismatch → failure with error message.
    /// </summary>
    Task<InstallResult> InstallAsync(
        string listingId,
        IProgress<InstallProgress>? progress = null,
        CancellationToken ct = default);

    /// <summary>Uninstalls the installed plugin identified by <paramref name="listingId"/>.</summary>
    Task<bool> UninstallAsync(string listingId, CancellationToken ct = default);

    // ── Installed / Updates ───────────────────────────────────────────────────

    /// <summary>Returns all currently installed marketplace plugins.</summary>
    Task<IReadOnlyList<MarketplaceListing>> GetInstalledAsync(CancellationToken ct = default);

    /// <summary>Returns installed plugins that have a newer version available.</summary>
    Task<IReadOnlyList<MarketplaceListing>> GetUpdatesAsync(CancellationToken ct = default);

    /// <summary>True when the plugin with <paramref name="listingId"/> is installed.</summary>
    bool IsInstalled(string listingId);

    // ── Integrity ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies the SHA256 of an installed plugin against the marketplace listing.
    /// Returns true when the hash matches (plugin is untampered).
    /// </summary>
    Task<bool> VerifyIntegrityAsync(string listingId, CancellationToken ct = default);

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Fires during download/install with progress updates.</summary>
    event EventHandler<InstallProgressEventArgs>? InstallProgressChanged;
}
