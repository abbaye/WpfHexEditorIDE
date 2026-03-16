// ==========================================================
// Project: WpfHexEditor.Core.AssemblyAnalysis
// File: Services/SourceLinkResolver.cs
// Author: Derek Tremblay
// Created: 2026-03-16
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Resolves local source file paths to SourceLink HTTP URLs.
//     SourceLink stores wildcard pattern → URL-template mappings in the PDB.
//     Example: "C:\src\myproject\*" → "https://raw.githubusercontent.com/user/repo/abc123/*"
//
// Architecture Notes:
//     Pattern: Service (stateless after construction).
//     BCL-only — no WPF, no NuGet.
// ==========================================================

namespace WpfHexEditor.Core.AssemblyAnalysis.Services;

/// <summary>
/// Resolves local file paths to their SourceLink HTTP URLs using the pattern
/// mappings embedded in a portable PDB.
/// </summary>
public sealed class SourceLinkResolver
{
    private readonly SourceLinkMap _map;

    public SourceLinkResolver(SourceLinkMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        _map = map;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Resolves <paramref name="localFilePath"/> to an HTTP URL using the SourceLink
    /// wildcard mappings.  Returns null when no pattern matches.
    /// <paramref name="line"/> is not embedded in the URL (caller appends `#Lnn` if needed).
    /// </summary>
    public string? ResolveUrl(string localFilePath)
    {
        if (string.IsNullOrEmpty(localFilePath)) return null;

        // Normalise to forward slashes for cross-platform comparison.
        var normalized = localFilePath.Replace('\\', '/');

        foreach (var (pattern, urlTemplate) in _map.Mappings)
        {
            var normalizedPattern = pattern.Replace('\\', '/');

            // SourceLink patterns always use a trailing '*' wildcard.
            var starIndex = normalizedPattern.IndexOf('*');
            if (starIndex < 0) continue;

            var prefix = normalizedPattern[..starIndex];

            if (!normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;

            var relative = normalized[prefix.Length..];
            var url      = urlTemplate.Replace("*", relative);
            return url;
        }

        return null;
    }

    /// <summary>
    /// Returns the SourceLink URL with a GitHub/AzDO line-anchor fragment appended.
    /// e.g. ".../Foo.cs#L42" for GitHub raw content links.
    /// </summary>
    public string? ResolveUrlWithLine(string localFilePath, int startLine)
    {
        var url = ResolveUrl(localFilePath);
        if (url is null || startLine <= 0) return url;

        // GitHub raw links: replace /raw/ with /blob/ so the fragment works.
        if (url.Contains("raw.githubusercontent.com"))
            url = url.Replace("raw.githubusercontent.com", "github.com")
                     .Replace("/raw/", "/blob/");

        return $"{url}#L{startLine}";
    }
}
