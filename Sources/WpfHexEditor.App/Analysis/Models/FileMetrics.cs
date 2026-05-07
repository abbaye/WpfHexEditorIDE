// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/Models/FileMetrics.cs
// Description: Aggregated metrics for a single source file.
// ==========================================================

namespace WpfHexEditor.App.Analysis.Models;

public sealed record FileMetrics
{
    public string   FilePath        { get; init; } = string.Empty;
    public string   FileName        { get; init; } = string.Empty;
    public string   ProjectName     { get; init; } = string.Empty;

    // Volume
    public int      TotalLines      { get; init; }
    public int      CodeLines       { get; init; }
    public int      BlankLines      { get; init; }
    public int      CommentLines    { get; init; }

    // Types
    public int      TypeCount       { get; init; }
    public int      MethodCount     { get; init; }
    public int      PropertyCount   { get; init; }

    // Complexity
    public int      MaxCyclomaticComplexity  { get; init; }
    public int      MaxCognitiveComplexity   { get; init; }
    public double   AvgCyclomaticComplexity  { get; init; }

    // Coupling
    public int      MaxDit          { get; init; }

    // Quality score for this file [0–100]
    public int      Score           { get; init; }

    public IReadOnlyList<MethodMetrics>  Methods    { get; init; } = [];
    public IReadOnlyList<CouplingMetrics> Couplings { get; init; } = [];
}
