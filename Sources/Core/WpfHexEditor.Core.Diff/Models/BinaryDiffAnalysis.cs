// Project      : WpfHexEditorControl
// File         : Models/BinaryDiffAnalysis.cs
// Description  : Entropy and byte-frequency analysis model for binary diff results.
// Architecture : Pure data model — no WPF, no I/O.

namespace WpfHexEditor.Core.Diff.Models;

/// <summary>
/// Shannon entropy and byte-frequency analysis for a binary diff pair.
/// Computed by <c>BinaryEntropyAnalyzer</c> on a background thread.
/// </summary>
public sealed class BinaryDiffAnalysis
{
    /// <summary>
    /// Shannon entropy per 256-byte block for the left file.
    /// Values in range [0.0 – 8.0]; higher = more random/compressed.
    /// </summary>
    public double[] EntropyLeft  { get; init; } = [];

    /// <summary>
    /// Shannon entropy per 256-byte block for the right file.
    /// Values in range [0.0 – 8.0].
    /// </summary>
    public double[] EntropyRight { get; init; } = [];

    /// <summary>Byte-value frequency table for the left file (256 entries, index = byte value).</summary>
    public int[] FreqLeft  { get; init; } = new int[256];

    /// <summary>Byte-value frequency table for the right file (256 entries, index = byte value).</summary>
    public int[] FreqRight { get; init; } = new int[256];

    /// <summary>
    /// Nibble (4-bit) frequency table for the left file (16 entries, index = nibble value 0-F).
    /// Computed as high-nibble frequency for histogram display.
    /// </summary>
    public int[] NibbleFreqLeft  { get; init; } = new int[16];

    /// <summary>
    /// Nibble (4-bit) frequency table for the right file (16 entries, index = nibble value 0-F).
    /// </summary>
    public int[] NibbleFreqRight { get; init; } = new int[16];

    /// <summary>Average entropy across all left-file blocks.</summary>
    public double AvgEntropyLeft  => EntropyLeft.Length  > 0 ? EntropyLeft.Average()  : 0.0;

    /// <summary>Average entropy across all right-file blocks.</summary>
    public double AvgEntropyRight => EntropyRight.Length > 0 ? EntropyRight.Average() : 0.0;
}
