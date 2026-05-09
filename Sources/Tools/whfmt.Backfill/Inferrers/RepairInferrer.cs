// ==========================================================
// Project: whfmt.Backfill
// File: Inferrers/RepairInferrer.cs
// Description: Infers a "repair" block (set of repair rules) from signatures + checksums.
// Architecture: Emits one rule per inferable trigger; null when no rules apply.
// ==========================================================

using WhfmtBackfill.Models;

namespace WhfmtBackfill.Inferrers;

/// <summary>One repair rule entry.</summary>
public sealed record RepairRule(
    string  Name,
    string  Trigger,
    string  Action,
    string? Target,
    string? Value,
    int?    Length,
    string? Algorithm,
    long?   DataOffset,
    int?    DataLength,
    string  Description);

/// <summary>Infers repair rules from a whfmt summary.</summary>
public static class RepairInferrer
{
    /// <summary>
    /// Emit:
    ///   • restore_magic for the first signature block with an expected value
    ///   • recompute_checksum for each declared checksum
    /// Returns an empty list (suppressed in emit) when no rules could be inferred.
    /// </summary>
    public static IReadOnlyList<RepairRule> Infer(WhfmtSummary s)
    {
        var rules = new List<RepairRule>();

        var sig = s.Blocks.FirstOrDefault(b => b.IsSignature && !string.IsNullOrEmpty(b.ExpectedValue));
        if (sig is not null)
        {
            string raw    = sig.ExpectedValue!.Trim();
            string hexVal = raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? raw[2..] : raw;
            // Only emit set_value when expectedValue parses as hex
            if (LooksHex(hexVal))
            {
                rules.Add(new RepairRule(
                    Name:        "FixSignature",
                    Trigger:     "SignatureMismatch",
                    Action:      "set_value",
                    Target:      sig.Offset.ToString(),
                    Value:       hexVal,
                    Length:      sig.Length,
                    Algorithm:   null,
                    DataOffset:  null,
                    DataLength:  null,
                    Description: $"Restore the {sig.Length}-byte {s.FormatId} signature at offset {sig.Offset}."));
            }
        }

        foreach (var c in s.Checksums)
        {
            if (string.IsNullOrEmpty(c.Algorithm)) continue;
            string algoLower = c.Algorithm.ToLowerInvariant();
            if (algoLower is not ("crc32" or "md5" or "sha1" or "sha256" or "adler32")) continue;

            rules.Add(new RepairRule(
                Name:        $"Recompute{c.Algorithm.ToUpperInvariant()}",
                Trigger:     $"{c.Algorithm.ToUpperInvariant()}Mismatch",
                Action:      "recompute_checksum",
                Target:      c.StoredOffset.ToString(),
                Value:       null,
                Length:      c.StoredLength > 0 ? c.StoredLength : null,
                Algorithm:   algoLower,
                DataOffset:  null,
                DataLength:  null,
                Description: $"Recompute {c.Algorithm.ToUpperInvariant()} and write at offset {c.StoredOffset}."));
        }

        return rules;
    }

    private static bool LooksHex(string s) =>
        s.Length > 0 && s.Length % 2 == 0 && s.All(c => Uri.IsHexDigit(c));
}
