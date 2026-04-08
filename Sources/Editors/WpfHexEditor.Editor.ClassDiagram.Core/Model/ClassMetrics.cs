// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: Model/ClassMetrics.cs
// Contributors: Claude Sonnet 4.6
// Created: 2026-04-07
// Description:
//     Immutable record holding software coupling and complexity metrics
//     for a single type node. Populated by RoslynClassDiagramAnalyzer.
//
// Architecture Notes:
//     Record for value-equality. All metrics are optional (null = not yet
//     computed). Instability follows Robert C. Martin's definition:
//     I = Ce / (Ca + Ce). I=0 maximally stable, I=1 maximally instable.
// ==========================================================

namespace WpfHexEditor.Editor.ClassDiagram.Core.Model;

/// <summary>
/// Software coupling and complexity metrics for a <see cref="ClassNode"/>.
/// Computed by the Roslyn analyzer; absent when using the regex fallback.
/// </summary>
public sealed record ClassMetrics
{
    /// <summary>
    /// Afferent coupling — number of types outside this type's namespace
    /// that depend on it. Higher = more stable (others rely on it).
    /// </summary>
    public int AfferentCoupling { get; init; }

    /// <summary>
    /// Efferent coupling — number of types outside this type's namespace
    /// that this type depends on. Higher = more instable.
    /// </summary>
    public int EfferentCoupling { get; init; }

    /// <summary>
    /// Instability index in [0.0, 1.0].
    /// I = Ce / (Ca + Ce). Returns 0 when both couplings are zero (fully stable).
    /// </summary>
    public double Instability =>
        (AfferentCoupling + EfferentCoupling) == 0
            ? 0.0
            : (double)EfferentCoupling / (AfferentCoupling + EfferentCoupling);

    /// <summary>
    /// Sum of cyclomatic complexity across all methods in this type.
    /// Computed via McCabe's formula: 1 + number of branching predicates per method.
    /// </summary>
    public int CyclomaticComplexity { get; init; }

    /// <summary>Total number of public members (API surface).</summary>
    public int PublicMemberCount { get; init; }

    /// <summary>Empty sentinel — metrics not computed.</summary>
    public static ClassMetrics Empty { get; } = new();
}
