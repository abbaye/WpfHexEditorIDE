// Project      : WpfHexEditorControl
// File         : Models/DiffEngineResult.cs
// Description  : Union result returned by DiffEngine — wraps either a binary or text result.
// Architecture : Pure model — no WPF, no I/O.

namespace WpfHexEditor.Core.Diff.Models;

/// <summary>
/// The combined result produced by <see cref="Services.DiffEngine.CompareAsync"/>.
/// Exactly one of <see cref="BinaryResult"/> or <see cref="TextResult"/> is non-null
/// depending on the effective <see cref="EffectiveMode"/>.
/// </summary>
public sealed class DiffEngineResult
{
    /// <summary>The mode that was actually used (may differ from requested when Auto was used or fallback occurred).</summary>
    public DiffMode         EffectiveMode  { get; init; }

    /// <summary>Non-null when <see cref="EffectiveMode"/> is <see cref="DiffMode.Binary"/>.</summary>
    public BinaryDiffResult? BinaryResult  { get; init; }

    /// <summary>Non-null when <see cref="EffectiveMode"/> is <see cref="DiffMode.Text"/> or <see cref="DiffMode.Semantic"/>.</summary>
    public TextDiffResult?   TextResult    { get; init; }

    /// <summary>Human-readable explanation when the engine fell back from a requested mode.</summary>
    public string?           FallbackReason { get; init; }

    /// <summary>Path of the left file that was compared.</summary>
    public string            LeftPath      { get; init; } = string.Empty;

    /// <summary>Path of the right file that was compared.</summary>
    public string            RightPath     { get; init; } = string.Empty;

    /// <summary>UTC timestamp when the comparison completed.</summary>
    public DateTime          CompletedUtc  { get; init; } = DateTime.UtcNow;

    /// <summary>Returns the similarity ratio (0–1) from whichever result is populated.</summary>
    public double Similarity =>
        BinaryResult?.Stats.Similarity ?? TextResult?.Stats.Similarity ?? 1.0;
}
