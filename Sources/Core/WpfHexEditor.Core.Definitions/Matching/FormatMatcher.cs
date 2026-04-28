//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using WpfHexEditor.Core.Contracts;

namespace WpfHexEditor.Core.Definitions.Matching;

/// <summary>
/// Stateless façade that combines extension, magic-byte, and MIME-type detection
/// into a single scored result or ranked list.
/// <para>
/// All methods are pure functions over <see cref="IEmbeddedFormatCatalog"/> — no
/// internal state is maintained. Thread-safe by construction.
/// </para>
/// </summary>
public static class FormatMatcher
{
    // ------------------------------------------------------------------
    // Single-result API
    // ------------------------------------------------------------------

    /// <summary>
    /// Returns the best <see cref="FormatMatchResult"/> for a file, combining
    /// extension and magic-byte detection.
    /// <para>
    /// Strategy (highest confidence first):
    /// <list type="number">
    ///   <item>Extension lookup → if the entry has signatures, confirm with <paramref name="header"/>.
    ///     Both agree → <see cref="MatchSource.Combined"/> confidence 1.0.</item>
    ///   <item>Magic-byte-only scan over the full catalog.</item>
    ///   <item>Extension-only (no signatures declared for that format).</item>
    /// </list>
    /// </para>
    /// </summary>
    /// <param name="catalog">The catalog to query.</param>
    /// <param name="extension">File extension including leading dot, e.g. <c>".zip"</c>.</param>
    /// <param name="header">
    /// First bytes of the file. Pass at least 16 bytes; 512 recommended.
    /// Pass <see cref="ReadOnlySpan{T}.Empty"/> to skip magic-byte detection.
    /// </param>
    public static FormatMatchResult? Match(
        IEmbeddedFormatCatalog catalog,
        string? extension,
        ReadOnlySpan<byte> header)
    {
        var byExt = string.IsNullOrEmpty(extension)
            ? null
            : catalog.GetByExtension(extension);

        if (header.IsEmpty)
            return byExt is null ? null : new FormatMatchResult(byExt, 0.5, MatchSource.Extension);

        var (byBytes, rawScore) = ScoreBest(catalog, header);

        // Extension + bytes agree → Combined
        if (byExt is not null && byBytes is not null && byExt.ResourceKey == byBytes.ResourceKey)
            return new FormatMatchResult(byExt, 1.0, MatchSource.Combined, rawScore);

        // Bytes found a better / different match
        if (byBytes is not null)
        {
            var conf = Math.Min(0.99, 0.5 + rawScore * 0.49);
            return new FormatMatchResult(byBytes, conf, MatchSource.MagicBytes, rawScore);
        }

        // Extension only
        if (byExt is not null)
            return new FormatMatchResult(byExt, 0.5, MatchSource.Extension);

        return null;
    }

    /// <summary>
    /// Returns the best match from a MIME type string.
    /// Confidence is fixed at 0.4 — MIME types are often generic (e.g. application/octet-stream).
    /// </summary>
    public static FormatMatchResult? MatchMime(IEmbeddedFormatCatalog catalog, string mimeType)
    {
        var entry = catalog.GetByMimeType(mimeType);
        return entry is null ? null : new FormatMatchResult(entry, 0.4, MatchSource.MimeType);
    }

    // ------------------------------------------------------------------
    // Multi-result API
    // ------------------------------------------------------------------

    /// <summary>
    /// Returns all entries that score above zero against <paramref name="header"/>,
    /// ordered by descending confidence. Useful for debugging ambiguous files.
    /// </summary>
    /// <param name="catalog">The catalog to query.</param>
    /// <param name="header">File header bytes.</param>
    /// <param name="maxResults">Maximum number of results to return. 0 = no limit.</param>
    public static IReadOnlyList<FormatMatchResult> GetTopMatches(
        IEmbeddedFormatCatalog catalog,
        ReadOnlySpan<byte> header,
        int maxResults = 5)
    {
        var results = new List<FormatMatchResult>();

        foreach (var entry in catalog.GetAll())
        {
            if (entry.Signatures is not { Count: > 0 }) continue;

            double score = 0;
            foreach (var sig in entry.Signatures)
            {
                var bytes = Convert.FromHexString(sig.Value);
                if (sig.Offset + bytes.Length > header.Length) continue;
                if (header.Slice(sig.Offset, bytes.Length).SequenceEqual(bytes))
                    score += sig.Weight;
            }

            if (score <= 0) continue;

            var conf = Math.Min(0.99, 0.5 + score * 0.49);
            results.Add(new FormatMatchResult(entry, conf, MatchSource.MagicBytes, score));
        }

        results.Sort();
        return maxResults > 0 ? results.Take(maxResults).ToList() : results;
    }

    /// <summary>
    /// Returns all entries matching a given extension (there may be several for
    /// ambiguous extensions such as <c>.bin</c>), ranked by quality score.
    /// </summary>
    public static IReadOnlyList<FormatMatchResult> GetMatchesByExtension(
        IEmbeddedFormatCatalog catalog,
        string extension)
    {
        var ext = extension.StartsWith('.') ? extension : '.' + extension;
        return catalog.GetAll()
            .Where(e => e.Extensions.Any(x => x.Equals(ext, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(e => e.QualityScore)
            .Select(e => new FormatMatchResult(e, 0.5, MatchSource.Extension))
            .ToList();
    }

    // ------------------------------------------------------------------
    // Private helpers
    // ------------------------------------------------------------------

    private static (EmbeddedFormatEntry? Entry, double Score) ScoreBest(
        IEmbeddedFormatCatalog catalog,
        ReadOnlySpan<byte> header)
    {
        EmbeddedFormatEntry? best = null;
        double bestScore = 0;

        foreach (var entry in catalog.GetAll())
        {
            if (entry.Signatures is not { Count: > 0 }) continue;
            double score = 0;
            foreach (var sig in entry.Signatures)
            {
                var bytes = Convert.FromHexString(sig.Value);
                if (sig.Offset + bytes.Length > header.Length) continue;
                if (header.Slice(sig.Offset, bytes.Length).SequenceEqual(bytes))
                    score += sig.Weight;
            }
            if (score > bestScore) { bestScore = score; best = entry; }
        }

        return (best, bestScore);
    }
}
