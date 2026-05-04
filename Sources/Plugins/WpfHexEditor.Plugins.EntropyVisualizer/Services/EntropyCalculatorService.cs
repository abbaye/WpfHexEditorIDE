// ==========================================================
// Project: WpfHexEditor.Plugins.EntropyVisualizer
// File: Services/EntropyCalculatorService.cs
// Description: Computes per-chunk Shannon entropy on a background thread.
//              No IDE dependency — safe for standalone use.
// Architecture Notes:
//     Chunking: configurable block size (default 256 bytes).
//     Results are cached by (length, sampled-checksum) to avoid re-scanning
//     an unchanged file on panel re-show.
// ==========================================================

using WpfHexEditor.Plugins.EntropyVisualizer.Models;

namespace WpfHexEditor.Plugins.EntropyVisualizer.Services;

internal sealed class EntropyCalculatorService
{
    // Value-equality cache key — not collision-resistant, good enough for same-session reuse.
    private record CacheKey(int Length, int ChunkSize, int Checksum);

    private CacheKey?                    _cacheKey;
    private IReadOnlyList<EntropyChunk>? _cachedResult;

    public Task<IReadOnlyList<EntropyChunk>> CalculateAsync(
        byte[]             data,
        int                chunkSize = 256,
        IProgress<double>? progress  = null,
        CancellationToken  ct        = default)
    {
        // All work including checksum runs on the thread-pool to keep the UI thread free.
        return Task.Run(() =>
        {
            var key = new CacheKey(data.Length, chunkSize, ComputeChecksum(data));
            if (_cacheKey == key && _cachedResult is not null)
            {
                progress?.Report(1.0);
                return _cachedResult;
            }

            var result = Calculate(data, chunkSize, progress, ct);
            _cacheKey     = key;
            _cachedResult = result;
            return result;
        }, ct);
    }

    private static IReadOnlyList<EntropyChunk> Calculate(
        byte[]             data,
        int                chunkSize,
        IProgress<double>? progress,
        CancellationToken  ct)
    {
        if (data.Length == 0) return [];

        int totalChunks = (data.Length + chunkSize - 1) / chunkSize;
        var chunks      = new List<EntropyChunk>(totalChunks);
        int lastReported = -1;

        for (int i = 0; i < totalChunks; i++)
        {
            ct.ThrowIfCancellationRequested();

            int offset = i * chunkSize;
            int length = Math.Min(chunkSize, data.Length - offset);
            double h   = ShannonEntropy(data, offset, length);

            chunks.Add(new EntropyChunk(i, offset, length, h));

            // Report at each whole-percent increment — avoids ~31K dispatcher posts for large files.
            int pct = (int)((double)i / totalChunks * 100);
            if (pct != lastReported)
            {
                progress?.Report((double)pct / 100);
                lastReported = pct;
            }
        }

        progress?.Report(1.0);
        return chunks;
    }

    private static double ShannonEntropy(byte[] data, int offset, int length)
    {
        if (length <= 1) return 0.0;

        Span<int> freq = stackalloc int[256];
        for (int i = 0; i < length; i++)
            freq[data[offset + i]]++;

        double entropy = 0.0;
        for (int b = 0; b < 256; b++)
        {
            if (freq[b] == 0) continue;
            double p  = (double)freq[b] / length;
            entropy  -= p * Math.Log2(p);
        }

        return entropy;
    }

    // Samples every 4096th byte — not collision-resistant; sufficient for same-session cache invalidation.
    private static int ComputeChecksum(byte[] data)
    {
        int xor = data.Length;
        for (int i = 0; i < data.Length; i += 4096)
            xor ^= (data[i] << (i % 24));
        return xor;
    }
}
