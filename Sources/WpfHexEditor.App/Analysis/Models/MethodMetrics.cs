// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/Models/MethodMetrics.cs
// Description: Per-method complexity and size metrics.
// ==========================================================

namespace WpfHexEditor.App.Analysis.Models;

public sealed class MethodMetrics
{
    public string Name               { get; init; } = string.Empty;
    public string FullyQualifiedName { get; init; } = string.Empty;
    public int    Line               { get; init; }
    public int    Loc                { get; init; }
    public int    CyclomaticComplexity  { get; init; }
    public int    CognitiveComplexity   { get; init; }
    public int    ParameterCount        { get; init; }
}
