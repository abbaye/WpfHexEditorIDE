// Project      : WpfHexEditorControl
// File         : Algorithms/IDiffAlgorithm.cs
// Description  : Contract for all diff algorithm implementations.
// Architecture : Strategy pattern — algorithms are interchangeable.

using WpfHexEditor.Core.Diff.Models;

namespace WpfHexEditor.Core.Diff.Algorithms;

/// <summary>
/// Strategy contract for diff algorithm implementations.
/// Implementations must be stateless (safe for concurrent calls).
/// </summary>
public interface IDiffAlgorithm
{
    /// <summary>
    /// Compute byte-level differences between two binary buffers.
    /// </summary>
    /// <param name="left">Bytes from the left (original) file.</param>
    /// <param name="right">Bytes from the right (modified) file.</param>
    /// <returns>A <see cref="BinaryDiffResult"/> describing all differing regions.</returns>
    BinaryDiffResult ComputeBytes(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right);

    /// <summary>
    /// Compute line-level differences between two text sequences.
    /// </summary>
    /// <param name="leftLines">Lines from the left (original) file.</param>
    /// <param name="rightLines">Lines from the right (modified) file.</param>
    /// <returns>A <see cref="TextDiffResult"/> describing all differing lines.</returns>
    TextDiffResult ComputeLines(string[] leftLines, string[] rightLines);
}
