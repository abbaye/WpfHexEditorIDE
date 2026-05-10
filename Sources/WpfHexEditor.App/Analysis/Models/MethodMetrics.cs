// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/Models/MethodMetrics.cs
// Description: Per-method complexity, size, and Halstead metrics.
// ==========================================================

namespace WpfHexEditor.App.Analysis.Models;

public sealed record MethodMetrics
{
    public string Name               { get; init; } = string.Empty;
    public string FullyQualifiedName { get; init; } = string.Empty;
    public int    Line               { get; init; }
    public int    Loc                { get; init; }

    // Complexity
    public int    CyclomaticComplexity  { get; init; }
    public int    CognitiveComplexity   { get; init; }
    public int    ParameterCount        { get; init; }

    // Halstead suite
    public int    HalsteadOperators        { get; init; }   // N1
    public int    HalsteadOperands         { get; init; }   // N2
    public int    HalsteadUniqueOperators  { get; init; }   // n1
    public int    HalsteadUniqueOperands   { get; init; }   // n2
    public double HalsteadVolume           { get; init; }
    public double HalsteadDifficulty       { get; init; }
    public double HalsteadEffort           { get; init; }
    public double HalsteadBugs             { get; init; }   // Volume / 3000

    /// <summary>Microsoft Maintainability Index 0–100 (higher = more maintainable).</summary>
    public double MaintainabilityIndex     { get; init; }
}
