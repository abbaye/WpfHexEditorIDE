// ==========================================================
// Project: whfmt.Fuzz
// File: FuzzReport.cs
// Description: Statistics and field coverage report for a fuzzing session.
// ==========================================================

namespace WhfmtFuzz;

/// <summary>Statistics and coverage report produced by <c>FormatFuzzer.GenerateWithReport</c>.</summary>
public sealed class FuzzReport
{
    /// <summary>Format name used for the session.</summary>
    public string FormatName { get; init; } = "";

    /// <summary>Total number of variants generated (including errors).</summary>
    public int TotalVariants { get; init; }

    /// <summary>Number of variants that failed to generate.</summary>
    public int ErrorCount { get; init; }

    /// <summary>Number of mutations applied per field across all variants.</summary>
    public IReadOnlyDictionary<string, int> FieldCoverage { get; init; } = new Dictionary<string, int>();

    /// <summary>Number of variants generated per mutation type.</summary>
    public IReadOnlyDictionary<MutationType, int> StrategyDistribution { get; init; } = new Dictionary<MutationType, int>();

    /// <summary>Fields declared in the whfmt blocks that have no fuzz strategy — untested.</summary>
    public IReadOnlyList<string> UntestedFields { get; init; } = [];

    /// <summary>Average number of mutations per variant (compound mode).</summary>
    public double AverageMutationsPerVariant { get; init; }

    /// <summary>Random seed used (null = random).</summary>
    public int? Seed { get; init; }

    /// <summary>Field targeted most frequently.</summary>
    public string? MostTargetedField => FieldCoverage.Count == 0 ? null
        : FieldCoverage.MaxBy(kv => kv.Value).Key;

    /// <summary>Mutation type used most frequently.</summary>
    public MutationType? DominantStrategy => StrategyDistribution.Count == 0 ? null
        : StrategyDistribution.MaxBy(kv => kv.Value).Key;

    /// <summary>Render a compact summary to a string.</summary>
    public override string ToString()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"  FuzzReport — {FormatName}");
        sb.AppendLine($"  Variants: {TotalVariants - ErrorCount} ok, {ErrorCount} errors");
        sb.AppendLine($"  Avg mutations/variant: {AverageMutationsPerVariant:F2}");
        if (MostTargetedField is not null)
            sb.AppendLine($"  Most targeted field: {MostTargetedField} ({FieldCoverage[MostTargetedField]}×)");
        if (DominantStrategy is not null)
            sb.AppendLine($"  Dominant strategy: {DominantStrategy}");
        if (UntestedFields.Count > 0)
            sb.AppendLine($"  Untested fields ({UntestedFields.Count}): {string.Join(", ", UntestedFields.Take(5))}{(UntestedFields.Count > 5 ? "…" : "")}");
        return sb.ToString();
    }
}
