// ==========================================================
// Project: WpfHexEditor.SDK
// File: Models/MarketplaceListing.cs
// Description:
//     v2 marketplace listing model — GitHub Releases API backed.
//     Promoted from [Obsolete] preview to stable SDK surface (SDK 3.0).
// ==========================================================

namespace WpfHexEditor.SDK.Models;

/// <summary>
/// Represents a plugin listing from the WpfHexEditor marketplace.
/// Populated from GitHub Releases API + local manifest merge.
/// </summary>
public sealed class MarketplaceListing
{
    // ── Identity ──────────────────────────────────────────────────────────────

    /// <summary>Unique marketplace listing identifier.</summary>
    public string ListingId { get; init; } = string.Empty;

    /// <summary>Plugin display name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Plugin description.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Publisher name.</summary>
    public string Publisher { get; init; } = string.Empty;

    /// <summary>Latest available version string (semver).</summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>Category (e.g. "Data Visualization", "Analysis").</summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>Searchable tags.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>License identifier (e.g. "AGPL-3.0-only", "MIT").</summary>
    public string License { get; init; } = string.Empty;

    /// <summary>Source repository URL (optional).</summary>
    public string? RepositoryUrl { get; init; }

    /// <summary>Icon URL or embedded base64 (optional).</summary>
    public string? Icon { get; init; }

    /// <summary>Whether this listing has been officially verified by the WpfHexEditor team.</summary>
    public bool Verified { get; init; }

    /// <summary>Download URL for the .whxplugin package.</summary>
    public string? DownloadUrl { get; init; }

    // ── GitHub Release metadata ───────────────────────────────────────────────

    /// <summary>GitHub owner/repo slug (e.g. "abbaye/WpfHexEditorControl").</summary>
    public string? GitHubRepo { get; init; }

    /// <summary>GitHub release ID used to pinpoint the correct asset.</summary>
    public long GitHubReleaseId { get; init; }

    /// <summary>Release notes / changelog body from GitHub.</summary>
    public string? ReleaseNotes { get; init; }

    /// <summary>Total GitHub download count for this listing.</summary>
    public int DownloadCount { get; init; }

    // ── Integrity ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Expected SHA-256 hash (hex, lowercase) of the downloaded .whxplugin.
    /// Empty when the publisher has not provided a checksum.
    /// </summary>
    public string Sha256 { get; init; } = string.Empty;

    // ── Installation state (populated at runtime) ─────────────────────────────

    /// <summary>Version currently installed, or null when not installed.</summary>
    public string? InstalledVersion { get; set; }

    /// <summary>True when <see cref="InstalledVersion"/> is not null.</summary>
    public bool IsInstalled => InstalledVersion is not null;

    /// <summary>True when the installed DLL passed its last SHA256 verification.</summary>
    public bool IsVerified { get; set; }

    /// <summary>True when a newer <see cref="Version"/> is available for an installed plugin.</summary>
    public bool HasUpdate =>
        IsInstalled
        && System.Version.TryParse(Version, out var latest)
        && System.Version.TryParse(InstalledVersion, out var installed)
        && latest > installed;
}
