// Project      : WpfHexEditorControl
// File         : Services/BinaryEntropyAnalyzer.cs
// Description  : O(n) Shannon entropy and byte-frequency analyzer for binary diff results.
// Architecture : Stateless static helper — no WPF, no I/O.  Safe for Task.Run.

using WpfHexEditor.Core.Diff.Models;

namespace WpfHexEditor.Core.Diff.Services;

/// <summary>
/// Computes Shannon entropy per 256-byte block and byte-value frequency tables
/// for a pair of binary buffers.
/// </summary>
public static class BinaryEntropyAnalyzer
{
    private const int BlockSize = 256;

    /// <summary>
    /// Analyzes both buffers and returns a <see cref="BinaryDiffAnalysis"/>.
    /// O(n) time and O(256) working space (plus output arrays).
    /// </summary>
    public static BinaryDiffAnalysis Analyze(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        var freqL   = ComputeFrequency(left);
        var freqR   = ComputeFrequency(right);
        var entL    = ComputeEntropy(left,  BlockSize);
        var entR    = ComputeEntropy(right, BlockSize);
        var nibL    = ComputeNibbleFreq(freqL);
        var nibR    = ComputeNibbleFreq(freqR);

        return new BinaryDiffAnalysis
        {
            EntropyLeft      = entL,
            EntropyRight     = entR,
            FreqLeft         = freqL,
            FreqRight        = freqR,
            NibbleFreqLeft   = nibL,
            NibbleFreqRight  = nibR
        };
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static int[] ComputeFrequency(ReadOnlySpan<byte> data)
    {
        var freq = new int[256];
        foreach (var b in data)
            freq[b]++;
        return freq;
    }

    private static double[] ComputeEntropy(ReadOnlySpan<byte> data, int blockSize)
    {
        if (data.Length == 0) return [];

        int blockCount = (data.Length + blockSize - 1) / blockSize;
        var entropy    = new double[blockCount];
        var localFreq  = new int[256];

        for (int b = 0; b < blockCount; b++)
        {
            int start = b * blockSize;
            int end   = Math.Min(start + blockSize, data.Length);
            int len   = end - start;

            // Reset local frequency
            Array.Clear(localFreq, 0, 256);
            for (int i = start; i < end; i++)
                localFreq[data[i]]++;

            // Shannon: H = -Σ p * log2(p)
            double h = 0.0;
            for (int v = 0; v < 256; v++)
            {
                if (localFreq[v] == 0) continue;
                double p = (double)localFreq[v] / len;
                h -= p * Math.Log2(p);
            }
            entropy[b] = h;
        }

        return entropy;
    }

    private static int[] ComputeNibbleFreq(int[] byteFreq)
    {
        var nibble = new int[16];
        for (int v = 0; v < 256; v++)
            nibble[v >> 4] += byteFreq[v];
        return nibble;
    }
}
