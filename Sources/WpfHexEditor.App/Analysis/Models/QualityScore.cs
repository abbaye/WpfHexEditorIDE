// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/Models/QualityScore.cs
// Description: Solution-level quality score with grade and trending delta.
// ==========================================================

namespace WpfHexEditor.App.Analysis.Models;

public sealed record QualityScore
{
    /// <summary>Overall score 0–100.</summary>
    public int    Score           { get; init; }
    /// <summary>Letter grade: A+, A, B+, B, C, D, F.</summary>
    public string Grade           { get; init; } = "?";
    /// <summary>Delta vs previous snapshot (positive = improvement).</summary>
    public int    TrendingDelta   { get; init; }

    // Sub-scores [0–100] used by the radar display
    public int    VolumeScore       { get; init; }
    public int    ComplexityScore   { get; init; }
    public int    CouplingScore     { get; init; }
    public int    DuplicationScore  { get; init; }
    public int    DeadCodeScore     { get; init; }
    public int    ConventionScore   { get; init; }

    public IReadOnlyList<FileMetrics> WorstFiles { get; init; } = [];

    /// <summary>Standard letter grade for a 0–100 score (single source of truth).</summary>
    public static string ToGrade(int score) => score switch
    {
        >= 93 => "A", >= 87 => "B+", >= 80 => "B",
        >= 70 => "C", >= 60 => "D",  _ => "F",
    };
}
