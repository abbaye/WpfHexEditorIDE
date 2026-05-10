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
    public int      MaxNoc          { get; init; }   // max number-of-children across types in file

    // Halstead aggregate (rolled up from methods)
    public double   HalsteadVolume   { get; init; }
    public double   HalsteadEffort   { get; init; }

    // Maintainability Index 0–100 (higher = more maintainable)
    public double   MaintainabilityIndex { get; init; }

    // Comment density (CommentLines / max(CodeLines,1)) * 100
    public double   CommentDensity   { get; init; }

    // LCOM4 cohesion: 1 = cohesive, > 1 = multiple components, > 5 = god class candidate
    public int      MaxLcom         { get; init; }

    // Hotspot flag (high complexity + frequent changes — set in Phase 5)
    public bool     IsHotspot       { get; init; }

    // Phase 5 — top contributor per file (git blame top author)
    public string   TopAuthor       { get; init; } = string.Empty;
    public int      ChangeCount     { get; init; }   // commits in last N days

    // Quality score for this file [0–100]
    public int      Score           { get; init; }

    public IReadOnlyList<MethodMetrics>  Methods    { get; init; } = [];
    public IReadOnlyList<CouplingMetrics> Couplings { get; init; } = [];
}
