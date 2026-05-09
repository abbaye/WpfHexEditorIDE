// ==========================================================
// Project: whfmt.Backfill
// File: Inferrers/DiffInferrer.cs
// Description: Infers a "diff" block (keyFields + ignoreFields) from existing block definitions.
// Architecture: Conservative rules; emits a serializable shape used by JsonBlockEmitter.
// ==========================================================

using WhfmtBackfill.Models;

namespace WhfmtBackfill.Inferrers;

/// <summary>Inferred diff block content; null when nothing reliable could be inferred.</summary>
public sealed record InferredDiff(IReadOnlyList<string> KeyFields, IReadOnlyList<string> IgnoreFields, string Note);

/// <summary>Infers a diff block from a parsed whfmt summary.</summary>
public static class DiffInferrer
{
    /// <summary>
    /// Build a diff block from blocks with known semantics.
    /// keyFields ← signature blocks + numeric magnitude blocks (size/count/width/height/version) + value-mapped enums
    /// ignoreFields ← time/date/stamp/padding/reserved/comment fields
    /// Returns null if no meaningful key fields could be derived.
    /// </summary>
    public static InferredDiff? Infer(WhfmtSummary s)
    {
        var keyFields    = new List<string>();
        var ignoreFields = new List<string>();
        var seen         = new HashSet<string>(StringComparer.Ordinal);

        foreach (var b in s.Blocks)
        {
            if (string.IsNullOrEmpty(b.StoreAs)) continue;
            string snake = NameNormalizer.ToSnakeCase(b.StoreAs);
            if (snake.Length == 0 || !seen.Add(snake)) continue;

            if (NameNormalizer.IsLikelyIgnored(snake))
            {
                ignoreFields.Add(snake);
                continue;
            }

            if (b.IsSignature || b.HasValueMap || b.HasBitfields)
            {
                keyFields.Add(snake);
                continue;
            }

            if (NameNormalizer.IsNumericValueType(b.ValueType) && NameNormalizer.IsNumericMagnitude(snake))
            {
                keyFields.Add(snake);
            }
        }

        // Always include known integrity fields if blocks reference them
        foreach (var c in s.Checksums)
        {
            string snake = NameNormalizer.ToSnakeCase(c.Algorithm);
            if (!string.IsNullOrEmpty(snake) && seen.Add(snake)) keyFields.Add(snake);
        }

        if (keyFields.Count == 0) return null;

        string note = $"Inferred semantic diff for {s.FormatId}. Compare key structural fields; ignore volatile metadata.";
        return new InferredDiff(keyFields, ignoreFields, note);
    }
}
