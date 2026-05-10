// ==========================================================
// Project: whfmt.Backfill
// File: Inferrers/FuzzInferrer.cs
// Description: Infers a "fuzz" block (mutation strategies) from existing block definitions.
// Architecture: Emits weighted strategies per dangerous field; null when no fuzzable fields.
// ==========================================================

using WhfmtBackfill.Models;

namespace WhfmtBackfill.Inferrers;

/// <summary>One fuzz strategy entry.</summary>
public sealed record FuzzStrategy(string Field, string Mutation, double? Rate, string Description);

/// <summary>Inferred fuzz block content.</summary>
public sealed record InferredFuzz(bool PreserveChecksums, int MaxMutationsPerFile, IReadOnlyList<FuzzStrategy> Strategies, string Note);

/// <summary>Infers fuzz strategies from a whfmt summary.</summary>
public static class FuzzInferrer
{
    public static InferredFuzz? Infer(WhfmtSummary s)
    {
        var strategies = new List<FuzzStrategy>();
        var seen       = new HashSet<string>(StringComparer.Ordinal);

        // Signature → corrupt_signature (weight: high)
        foreach (var b in s.Blocks.Where(b => b.IsSignature))
        {
            string snake = NameNormalizer.ToSnakeCase(string.IsNullOrEmpty(b.StoreAs) ? b.Name : b.StoreAs);
            if (string.IsNullOrEmpty(snake) || !seen.Add(snake)) continue;
            strategies.Add(new FuzzStrategy(snake, "corrupt_signature", null,
                $"Corrupt magic bytes at offset {b.Offset} — parser must reject."));
        }

        // Enum / bitfield blocks → enum_sweep
        foreach (var b in s.Blocks.Where(b => (b.HasValueMap || b.HasBitfields) && !b.IsSignature))
        {
            if (string.IsNullOrEmpty(b.StoreAs)) continue;
            string snake = NameNormalizer.ToSnakeCase(b.StoreAs);
            if (string.IsNullOrEmpty(snake) || !seen.Add(snake)) continue;
            strategies.Add(new FuzzStrategy(snake, "enum_sweep", null,
                "Sweep all valid enum codes plus reserved/invalid values."));
        }

        // Numeric magnitude blocks → boundary_values
        foreach (var b in s.Blocks)
        {
            if (string.IsNullOrEmpty(b.StoreAs)) continue;
            if (b.IsSignature || b.HasValueMap || b.HasBitfields) continue;
            if (!NameNormalizer.IsNumericValueType(b.ValueType)) continue;
            string snake = NameNormalizer.ToSnakeCase(b.StoreAs);
            if (string.IsNullOrEmpty(snake) || !seen.Add(snake)) continue;
            if (!NameNormalizer.IsNumericMagnitude(snake)) continue;

            strategies.Add(new FuzzStrategy(snake, "boundary_values", null,
                "Apply 0, 1, MAX-1, MAX boundaries — exposes overflow / underflow paths."));
        }

        // Checksum fields → random_bytes (will fail validation, exposes integrity-check paths)
        foreach (var c in s.Checksums)
        {
            string snake = NameNormalizer.ToSnakeCase(c.Algorithm);
            if (string.IsNullOrEmpty(snake) || !seen.Add(snake)) continue;
            strategies.Add(new FuzzStrategy(snake, "random_bytes", null,
                $"Random {c.Algorithm.ToUpperInvariant()} value triggers integrity failure."));
        }

        if (strategies.Count == 0) return null;

        return new InferredFuzz(
            PreserveChecksums:   s.Checksums.Count > 0,
            MaxMutationsPerFile: 1,
            Strategies:          strategies,
            Note:                $"Inferred fuzz strategies for {s.FormatId}. Hand-tune weights/rates per format if needed.");
    }
}
