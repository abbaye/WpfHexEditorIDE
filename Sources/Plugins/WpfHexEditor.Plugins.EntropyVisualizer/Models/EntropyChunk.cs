// ==========================================================
// Project: WpfHexEditor.Plugins.EntropyVisualizer
// File: Models/EntropyChunk.cs
// Description: Represents one entropy measurement block.
// ==========================================================

namespace WpfHexEditor.Plugins.EntropyVisualizer.Models;

/// <param name="BlockIndex">Zero-based chunk index.</param>
/// <param name="Offset">Byte offset of the first byte in this chunk.</param>
/// <param name="Length">Number of bytes in this chunk.</param>
/// <param name="Entropy">Shannon entropy (0.0 – 8.0 bits).</param>
internal sealed record EntropyChunk(int BlockIndex, long Offset, int Length, double Entropy)
{
    public bool IsHighEntropy => Entropy >= 7.2;
}
