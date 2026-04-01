// Project      : WpfHexEditorControl
// File         : Models/BinaryDiffOptions.cs
// Description  : Options controlling binary diff algorithm selection and output.
// Architecture : Pure model — no WPF, no I/O.

namespace WpfHexEditor.Core.Diff.Models;

/// <summary>
/// Options for the binary comparison path of <c>DiffEngine</c>.
/// </summary>
public sealed class BinaryDiffOptions
{
    /// <summary>Default options: byte-scan algorithm, full bytes not retained.</summary>
    public static readonly BinaryDiffOptions Default = new();

    /// <summary>
    /// When <see langword="true"/>, the full raw byte arrays of both files are attached
    /// to <see cref="BinaryDiffResult.FullLeftBytes"/> / <see cref="BinaryDiffResult.FullRightBytes"/>
    /// so that <c>BinaryHexRowBuilder</c> can reconstruct equal gaps for the hex dump view.
    /// <para>
    /// Memory cost: up to 2 × file size (capped at the engine's 50 MB limit per side).
    /// Enable only when displaying the hex dump diff view.
    /// </para>
    /// </summary>
    public bool RetainFullBytes { get; init; } = false;

    /// <summary>
    /// When <see langword="true"/>, use the block-aligned Rabin-Karp + LCS algorithm
    /// (<c>BlockAlignedBinaryAlgorithm</c>) instead of the default byte-scan algorithm.
    /// This correctly detects true insertions and deletions at the cost of slightly
    /// higher CPU usage.
    /// </summary>
    public bool UseBlockAlignment { get; init; } = false;

    /// <summary>
    /// Block size (in bytes) used by the Rabin-Karp rolling hash when
    /// <see cref="UseBlockAlignment"/> is <see langword="true"/>.
    /// Ignored for the default byte-scan algorithm.
    /// </summary>
    public int BlockSize { get; init; } = 64;
}
