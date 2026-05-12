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
    /// Honors detection v3 fields (matchMode, minimumScore, minFileSize).
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
            var score = ScoreEntry(entry, header);
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
            var score = ScoreEntry(entry, header);
            if (score > bestScore) { bestScore = score; best = entry; }
        }

        return (best, bestScore);
    }

    /// <summary>
    /// Computes the magic-byte score for a single catalog entry against
    /// <paramref name="header"/>. Honors detection v3 fields:
    /// <list type="bullet">
    ///   <item><c>MinFileSize</c> — entries requiring a larger header are skipped.</item>
    ///   <item><c>MatchMode</c> — "any" short-circuits, "all" requires every signature, "best" sums weights.</item>
    ///   <item><c>MinimumScore</c> — score below the declared threshold is discarded.</item>
    /// </list>
    /// Returns 0 when no match. Malformed hex signatures are skipped (do not throw).
    /// </summary>
    internal static double ScoreEntry(EmbeddedFormatEntry entry, ReadOnlySpan<byte> header)
    {
        if (entry.Signatures is not { Count: > 0 }) return 0;
        if (entry.MinFileSize > 0 && header.Length < entry.MinFileSize) return 0;

        bool isAny = string.Equals(entry.MatchMode, "any", StringComparison.OrdinalIgnoreCase);
        bool isAll = string.Equals(entry.MatchMode, "all", StringComparison.OrdinalIgnoreCase);

        int matched = 0;
        double score = 0;
        foreach (var sig in entry.Signatures)
        {
            if (sig.Value.Length == 0 || (sig.Value.Length & 1) != 0) continue;
            byte[] bytes;
            try { bytes = Convert.FromHexString(sig.Value); }
            catch (FormatException) { continue; }

            if (sig.Offset + bytes.Length > header.Length) continue;
            if (header.Slice(sig.Offset, bytes.Length).SequenceEqual(bytes))
            {
                matched++;
                score += sig.Weight;
                if (isAny) break;
            }
        }

        if (matched == 0) return 0;
        if (isAll && matched != entry.Signatures.Count) return 0;
        if (entry.MinimumScore > 0 && score < entry.MinimumScore) return 0;
        return score;
    }
}
