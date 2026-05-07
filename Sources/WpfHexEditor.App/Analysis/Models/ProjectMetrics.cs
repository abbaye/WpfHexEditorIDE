// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/Models/ProjectMetrics.cs
// Description: Aggregated metrics for a single project.
// ==========================================================

namespace WpfHexEditor.App.Analysis.Models;

public sealed class ProjectMetrics
{
    public string   ProjectName     { get; init; } = string.Empty;
    public string   ProjectPath     { get; init; } = string.Empty;

    public int      TotalFiles      { get; init; }
    public int      TotalLines      { get; init; }
    public int      CodeLines       { get; init; }
    public int      TypeCount       { get; init; }
    public int      MethodCount     { get; init; }

    public double   AvgCyclomaticComplexity { get; init; }
    public int      MaxCyclomaticComplexity { get; init; }

    public double   DuplicationPercent      { get; init; }
    public int      DeadSymbolCount         { get; init; }

    public int      Score           { get; init; }
    public string   Grade           { get; init; } = "?";

    public IReadOnlyList<FileMetrics> Files { get; init; } = [];
}
