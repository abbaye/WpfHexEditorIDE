// ==========================================================
// Project: WpfHexEditor.PluginHost
// File: Services/PluginIntegrityService.cs
// Description:
//     SHA-256 verification for downloaded plugin packages.
//     Used in the MarketplaceServiceImpl install pipeline.
// Architecture Notes:
//     Pure computation — no I/O beyond file reads. Thread-safe (stateless).
// ==========================================================

using System.IO;
using System.Security.Cryptography;

namespace WpfHexEditor.PluginHost.Services;

/// <summary>
/// Verifies and computes SHA-256 hashes for plugin package files.
/// </summary>
internal static class PluginIntegrityService
{
    /// <summary>
    /// Verifies that <paramref name="filePath"/> matches <paramref name="expectedSha256"/>.
    /// Returns true when the hashes match, false on mismatch.
    /// Throws <see cref="FileNotFoundException"/> if the file does not exist.
    /// </summary>
    internal static async Task<bool> VerifyAsync(
        string filePath,
        string expectedSha256,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(expectedSha256)) return true; // no checksum provided → skip
        var actual = await ComputeAsync(filePath, ct);
        return string.Equals(actual, expectedSha256, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Computes the SHA-256 hash of <paramref name="filePath"/> and returns it as lowercase hex.
    /// </summary>
    internal static async Task<string> ComputeAsync(
        string filePath,
        CancellationToken ct = default)
    {
        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);

        var hash = await SHA256.HashDataAsync(stream, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
