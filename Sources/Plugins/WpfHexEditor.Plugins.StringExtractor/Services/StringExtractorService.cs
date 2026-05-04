// ==========================================================
// Project: WpfHexEditor.Plugins.StringExtractor
// File: Services/StringExtractorService.cs
// Description: Core extraction logic — scans raw bytes for printable string
//              sequences in ASCII, UTF-8, UTF-16 LE/BE encodings.
//              No IDE dependency; safe for standalone use.
// ==========================================================

using System.Text;
using WpfHexEditor.Plugins.StringExtractor.Models;

namespace WpfHexEditor.Plugins.StringExtractor.Services;

internal sealed class StringExtractorService
{
    // Printable ASCII range: 0x20–0x7E plus tab/LF/CR accepted as run-extenders.
    private static bool IsAsciiPrintable(byte b)
        => b is >= 0x20 and <= 0x7E or 0x09 or 0x0A or 0x0D;

    /// <summary>
    /// Extracts all printable strings from <paramref name="data"/> according to
    /// <paramref name="options"/>. Returns a sorted, deduplicated list.
    /// Runs on a background thread; use <c>Task.Run</c> at the call site.
    /// </summary>
    public Task<IReadOnlyList<ExtractedString>> ExtractAsync(
        byte[]                  data,
        StringExtractionOptions options,
        IProgress<double>?      progress = null,
        CancellationToken       ct       = default)
        => Task.Run(() => Extract(data, options, progress, ct), ct);

    private static IReadOnlyList<ExtractedString> Extract(
        byte[]                  data,
        StringExtractionOptions options,
        IProgress<double>?      progress,
        CancellationToken       ct)
    {
        if (data.Length == 0) return [];

        var results = new List<ExtractedString>();

        if (options.ScanAscii)   ScanAscii(data, options.MinLength, results);
        if (options.ScanUtf8)    ScanUtf8(data, options.MinLength, results);
        if (options.ScanUtf16Le) ScanUtf16(data, options.MinLength, bigEndian: false, results);
        if (options.ScanUtf16Be) ScanUtf16(data, options.MinLength, bigEndian: true, results);

        ct.ThrowIfCancellationRequested();

        // Deduplicate by (offset, encoding) — different encodings at the same offset are kept.
        var seen    = new HashSet<(long, string)>();
        var ordered = results
            .Where(r => seen.Add((r.Offset, r.Encoding)))
            .OrderBy(r => r.Offset)
            .ToList();

        progress?.Report(1.0);
        return ordered;
    }

    private static void ScanAscii(byte[] data, int minLen, List<ExtractedString> results)
    {
        int start = -1;
        for (int i = 0; i <= data.Length; i++)
        {
            bool printable = i < data.Length && IsAsciiPrintable(data[i]);
            if (printable)
            {
                if (start < 0) start = i;
            }
            else if (start >= 0)
            {
                FlushRun(data, start, i - start, minLen, Encoding.ASCII, "ASCII", results);
                start = -1;
            }
        }
    }

    private static void ScanUtf8(byte[] data, int minLen, List<ExtractedString> results)
    {
        var decoder = Encoding.UTF8.GetDecoder();
        // Fixed-size buffer: UTF-8 sequences are at most 4 bytes → at most 4 chars.
        var charBuf = new char[16];
        int start   = -1;
        int i       = 0;

        while (i < data.Length)
        {
            int seqLen = GetUtf8SequenceLength(data[i]);
            if (seqLen < 1 || i + seqLen > data.Length)
            {
                FlushUtf8Run(data, start, i, minLen, results);
                start = -1;
                i++;
                continue;
            }

            try
            {
                int charCount    = decoder.GetChars(data, i, seqLen, charBuf, 0);
                bool allPrintable = true;
                for (int c = 0; c < charCount; c++)
                {
                    if (charBuf[c] < 0x20 && charBuf[c] is not '\t' and not '\n' and not '\r')
                    { allPrintable = false; break; }
                }

                if (allPrintable)
                {
                    if (start < 0) start = i;
                    i += seqLen;
                }
                else
                {
                    FlushUtf8Run(data, start, i, minLen, results);
                    start = -1;
                    i += seqLen;
                }
            }
            catch
            {
                // Malformed UTF-8 — reset decoder state and skip byte to resync.
                FlushUtf8Run(data, start, i, minLen, results);
                start = -1;
                i++;
                decoder.Reset();
            }
        }
        FlushUtf8Run(data, start, data.Length, minLen, results);
    }

    private static void FlushUtf8Run(byte[] data, int start, int end, int minLen, List<ExtractedString> results)
    {
        if (start < 0) return;
        int len = end - start;
        if (len < minLen) return;
        try
        {
            var value = Encoding.UTF8.GetString(data, start, len).TrimEnd('\r', '\n');
            if (!string.IsNullOrWhiteSpace(value) && value.Length >= minLen)
                results.Add(new ExtractedString(start, len, "UTF-8", value));
        }
        catch { /* malformed sequence — skip */ }
    }

    private static int GetUtf8SequenceLength(byte b) => b switch
    {
        < 0x80 => 1,
        < 0xC0 => 0,  // continuation byte as lead is invalid
        < 0xE0 => 2,
        < 0xF0 => 3,
        < 0xF8 => 4,
        _      => 0
    };

    private static void ScanUtf16(byte[] data, int minLen, bool bigEndian, List<ExtractedString> results)
    {
        if (data.Length < 2) return;

        var encoding     = bigEndian ? Encoding.BigEndianUnicode : Encoding.Unicode;
        var encodingName = bigEndian ? "UTF-16 BE" : "UTF-16 LE";
        int start        = -1;

        for (int i = 0; i + 1 < data.Length; i += 2)
        {
            char c = bigEndian
                ? (char)((data[i] << 8) | data[i + 1])
                : (char)((data[i + 1] << 8) | data[i]);

            bool printable = c >= 0x20 && c <= 0xFFFD && !char.IsControl(c);
            if (printable)
            {
                if (start < 0) start = i;
            }
            else if (start >= 0)
            {
                FlushRun(data, start, i - start, minLen, encoding, encodingName, results);
                start = -1;
            }
        }

        if (start >= 0)
        {
            int byteLen = data.Length - start - (data.Length - start) % 2;
            FlushRun(data, start, byteLen, byteLen / 2, encoding, encodingName, results);
        }
    }

    // Shared flush helper for ASCII and UTF-16 runs.
    private static void FlushRun(byte[] data, int start, int byteLen, int minCharLen,
                                  Encoding enc, string encName, List<ExtractedString> results)
    {
        if (start < 0 || byteLen <= 0) return;
        var value = enc.GetString(data, start, byteLen).TrimEnd('\r', '\n');
        if (!string.IsNullOrWhiteSpace(value) && value.Length >= minCharLen)
            results.Add(new ExtractedString(start, byteLen, encName, value));
    }
}
