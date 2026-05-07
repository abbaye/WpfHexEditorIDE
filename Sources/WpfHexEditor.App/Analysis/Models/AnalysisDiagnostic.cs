// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/Models/AnalysisDiagnostic.cs
// Description: A single diagnostic produced by the analysis pipeline.
//              WH0xxx = custom quality rules; CS/IDE = Roslyn compiler.
// ==========================================================

namespace WpfHexEditor.App.Analysis.Models;

public enum DiagnosticSeverity { Info, Warning, Error }

/// <summary>A single quality or compiler diagnostic with source location.</summary>
public sealed class AnalysisDiagnostic
{
    public string           Id          { get; init; } = string.Empty;
    public DiagnosticSeverity Severity  { get; init; }
    public string           Message     { get; init; } = string.Empty;
    public string           FilePath    { get; init; } = string.Empty;
    public int              Line        { get; init; } = -1;
    public int              Column      { get; init; } = -1;
    public string           ProjectName { get; init; } = string.Empty;
    public string           RuleSource  { get; init; } = string.Empty; // "Roslyn" | "Quality"
}
