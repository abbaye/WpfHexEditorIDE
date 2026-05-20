//////////////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project: WpfHexEditor.App
// File: BinaryAnalysis/Services/EntropyService.cs
// Description: Shannon entropy computation over byte ranges.
//              Pure static, no I/O, no WPF dependencies — standalone-safe.
//////////////////////////////////////////////////////

namespace WpfHexEditor.App.BinaryAnalysis.Services;

/// <summary>Block size used for one entropy sample.</summary>
public enum EntropyWindowSize
{
    Small  = 128,
    Medium = 256,
    Large  = 512,
}

/// <summary>
/// Computes Shannon entropy on byte windows.
/// All methods are pure — no I/O, no allocations beyond the return value.
/// </summary>
public static class EntropyService
{
    /// <summary>
    /// Computes Shannon entropy of <paramref name="window"/> in bits/byte (0.0 – 8.0).
    /// </summary>
    public static double ComputeEntropy(ReadOnlySpan<byte> window)
    {
        if (window.IsEmpty) return 0.0;

        Span<int> freq = stackalloc int[256];
        foreach (byte b in window)
            freq[b]++;

        double entropy = 0.0;
        double len = window.Length;
        for (int i = 0; i < 256; i++)
        {
            if (freq[i] == 0) continue;
            double p = freq[i] / len;
            entropy -= p * Math.Log2(p);
        }
        return entropy;
    }

    /// <summary>
    /// Reads <paramref name="filePath"/> and returns one entropy sample per
    /// <paramref name="windowSize"/> bytes, covering [<paramref name="startOffset"/>,
    /// <paramref name="startOffset"/> + <paramref name="length"/>).
    /// Returns empty list if the file cannot be read.
    /// </summary>
    public static List<EntropyBlock> ComputeRange(
        string filePath,
        long   startOffset,
        long   length,
        int    windowSize)
    {
        var result = new List<EntropyBlock>((int)(length / windowSize) + 1);
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return result;
        if (windowSize <= 0) windowSize = (int)EntropyWindowSize.Medium;

        try
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            fs.Position = startOffset;

            Span<byte> buf = stackalloc byte[windowSize];
            long pos = startOffset;
            long end = startOffset + length;

            while (pos < end)
            {
                long remaining = end - pos;
                int toRead     = (int)Math.Min(windowSize, remaining);
                var slice      = buf[..toRead];
                int read       = fs.Read(slice);
                if (read == 0) break;

                double entropy = ComputeEntropy(slice[..read]);
                result.Add(new EntropyBlock(pos, read, entropy));
                pos += read;
            }
        }
        catch { /* non-fatal: returns partial results */ }

        return result;
    }
}

/// <summary>One entropy sample: file offset, byte count, and Shannon score.</summary>
public readonly record struct EntropyBlock(long Offset, int Length, double Entropy);
