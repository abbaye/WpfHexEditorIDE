// ==========================================================
// Project: whfmt.Analysis
// File: DiffResult.cs
// Description: Immutable result model for a semantic format diff.
// ==========================================================

namespace WhfmtAnalysis;

/// <summary>Result of a semantic field-level comparison between two binary files.</summary>
public sealed class DiffResult
{
    /// <summary>Path or name of the first file (A).</summary>
    public string FileA { get; init; } = "";

    /// <summary>Path or name of the second file (B).</summary>
    public string FileB { get; init; } = "";

    /// <summary>Byte size of file A.</summary>
    public long SizeA { get; init; }

    /// <summary>Byte size of file B.</summary>
    public long SizeB { get; init; }

    /// <summary>Detected format name (shared).</summary>
    public string FormatName { get; set; } = "Unknown";

    /// <summary>Format detected for file A.</summary>
    public string FormatDetectedA { get; set; } = "Unknown";

    /// <summary>Format detected for file B.</summary>
    public string FormatDetectedB { get; set; } = "Unknown";

    /// <summary>True when both files were identified as the same format.</summary>
    public bool FormatsMatch { get; set; }

    /// <summary>Key fields used for comparison (from diff.keyFields).</summary>
    public List<string> KeyFields { get; set; } = [];

    /// <summary>Fields excluded from comparison (from diff.ignoreFields).</summary>
    public List<string> IgnoreFields { get; set; } = [];

    /// <summary>Grouping field name (from diff.groupBy), or null.</summary>
    public string? GroupBy { get; set; }

    /// <summary>All field comparisons — key fields and ignored fields.</summary>
    public List<FieldChange> FieldChanges { get; } = [];

    /// <summary>Raw byte size delta (B.Length - A.Length).</summary>
    public long RawByteDelta { get; set; }

    /// <summary>True when all key fields are equal and sizes match.</summary>
    public bool IsIdentical { get; set; }

    /// <summary>Error message if diff could not be completed, otherwise null.</summary>
    public string? Error { get; set; }

    /// <summary>Number of changed key fields.</summary>
    public int ChangedCount => FieldChanges.Count(f => !f.IsIgnored && f.IsChanged);

    /// <summary>Number of unchanged key fields.</summary>
    public int UnchangedCount => FieldChanges.Count(f => !f.IsIgnored && !f.IsChanged);
}

/// <summary>Comparison result for a single named field.</summary>
public sealed class FieldChange
{
    /// <summary>Field / variable name from the whfmt definition.</summary>
    public string FieldName { get; init; } = "";

    /// <summary>Value in file A (formatted as string).</summary>
    public string ValueA { get; init; } = "";

    /// <summary>Value in file B (formatted as string).</summary>
    public string ValueB { get; init; } = "";

    /// <summary>True when values differ between A and B.</summary>
    public bool IsChanged { get; init; }

    /// <summary>True when this field is in the ignore list — shown but not counted as a change.</summary>
    public bool IsIgnored { get; init; }
}
