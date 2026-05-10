// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/Models/CouplingMetrics.cs
// Description: Ca/Ce/Instability/LCOM coupling metrics for a type.
// ==========================================================

namespace WpfHexEditor.App.Analysis.Models;

public enum LcomLevel { Low, Medium, High }

public sealed class CouplingMetrics
{
    public string    TypeName     { get; init; } = string.Empty;
    public string    FilePath     { get; init; } = string.Empty;
    public int       Line         { get; init; }
    /// <summary>Afferent coupling — how many types depend on this type.</summary>
    public int       Ca           { get; init; }
    /// <summary>Efferent coupling — how many types this type depends on.</summary>
    public int       Ce           { get; init; }
    /// <summary>Instability = Ce / (Ca + Ce). Range [0,1]. 1 = maximally unstable.</summary>
    public double    Instability  { get; init; }
    public LcomLevel Lcom         { get; init; }

    /// <summary>FQ type names this type depends on (efferent set).</summary>
    public IReadOnlyList<string> DependsOn { get; init; } = [];
}
