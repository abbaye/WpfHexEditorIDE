//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Core.Contracts;

/// <summary>
/// Represents a single format match produced by <c>FormatMatcher</c>.
/// Immutable and sortable by descending confidence.
/// </summary>
/// <param name="Entry">The matched format entry.</param>
/// <param name="Confidence">
/// Normalised confidence in [0.0, 1.0].
/// <list type="bullet">
///   <item><description>1.0 — extension + magic-byte confirmed (Combined)</description></item>
///   <item><description>0.8–0.99 — magic-byte score only (multiple signatures may push above 1 internally, clamped)</description></item>
///   <item><description>0.5 — extension-only (no signatures available)</description></item>
///   <item><description>0.4 — MIME-type-only</description></item>
/// </list>
/// </param>
/// <param name="Source">Which detection strategy produced this match.</param>
/// <param name="RawScore">
/// Raw accumulated signature weight before normalisation. Useful for debugging.
/// 0.0 when <see cref="Source"/> does not include <see cref="MatchSource.MagicBytes"/>.
/// </param>
public sealed record FormatMatchResult(
    EmbeddedFormatEntry Entry,
    double Confidence,
    MatchSource Source,
    double RawScore = 0.0)
    : IComparable<FormatMatchResult>
{
    /// <summary>
    /// Returns <see langword="true"/> when the match was confirmed by both extension
    /// and magic-byte signatures.
    /// </summary>
    public bool IsConfirmed => Source.HasFlag(MatchSource.Combined);

    /// <summary>
    /// Compares by descending confidence (higher confidence = "less than" for sort purposes,
    /// so that <c>list.Sort()</c> naturally puts the best match first).
    /// </summary>
    public int CompareTo(FormatMatchResult? other)
    {
        if (other is null) return -1;
        return other.Confidence.CompareTo(Confidence); // descending
    }

    /// <inheritdoc/>
    public override string ToString()
        => $"{Entry.Name} [{Source}] {Confidence:P0} (raw={RawScore:F2})";
}
