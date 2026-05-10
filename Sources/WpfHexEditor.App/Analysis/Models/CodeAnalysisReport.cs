// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/Models/CodeAnalysisReport.cs
// Description: Root snapshot produced by a single code analysis run.
// ==========================================================

namespace WpfHexEditor.App.Analysis.Models;

public sealed class CodeAnalysisReport
{
    public DateTime  Timestamp       { get; init; } = DateTime.UtcNow;
    public string    SolutionPath    { get; init; } = string.Empty;
    public AnalysisScope Scope       { get; init; } = AnalysisScope.Solution;
    public string    ScopePath       { get; init; } = string.Empty;

    // Aggregates
    public int       TotalFiles      { get; init; }
    public int       TotalLines      { get; init; }
    public int       ProjectCount    { get; init; }

    public QualityScore                    Score        { get; init; } = new();
    public IReadOnlyList<ProjectMetrics>   Projects     { get; init; } = [];
    public IReadOnlyList<AnalysisDiagnostic> Diagnostics { get; init; } = [];
    public IReadOnlyList<DuplicationGroup> Duplications { get; init; } = [];
    public IReadOnlyList<DeadSymbol>       DeadSymbols  { get; init; } = [];
    public IReadOnlyList<ProjectCycleInfo> ProjectCycles { get; init; } = [];

    public bool IsEmpty => TotalFiles == 0;
}

public sealed class ProjectCycleInfo
{
    public IReadOnlyList<string> Projects { get; init; } = [];
    public string Display => string.Join(" → ", Projects) + " → " + (Projects.Count > 0 ? Projects[0] : "?");
}
