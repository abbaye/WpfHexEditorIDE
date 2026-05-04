// ==========================================================
// Project: WpfHexEditor.Plugins.StringExtractor
// File: Services/StringExtractorService.cs
// Description: Core extraction logic — scans raw bytes for printable string
//              sequences in ASCII, UTF-8, UTF-16 LE/BE encodings.
//              No IDE dependency; safe for standalone use.
// Architecture Notes:
//     Returns IAsyncEnumerable<ExtractedString> for memory-efficient streaming
//     over large files. Caller cancels via CancellationToken.
// ==========================================================

using System.Text;
using WpfHexEditor.Plugins.StringExtractor.Models;

namespace WpfHexEditor.Plugins.StringExtractor.Services;

internal sealed class StringExtractorService
{
    // Printable ASCII range: 0x20 (space) to 0x7E (~), plus tab (0x09) and newline (0x0A/0x0D).
    private static bool IsAsciiPrintable(byte b)
        => b is >= 0x20 and <= 0x7E or 0x09 or 0x0A or 0x0D;

    /// <summary>
    /// Extracts all printable strings from <paramref name="data"/> according to
    /// <paramref name="options"/>. Runs synchronously on the provided buffer;
    /// the async enumerable boundary keeps the API composable with UI tasks.
    /// </summary>
    public async IAsyncEnumerable<ExtractedString> ExtractAsync(
        byte[]                   data,
        StringExtractionOptions  options,
        IProgress<double>?       progress    = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken        ct          = default)
    {
        if (data.Length == 0) yield break;

        var results = new List<ExtractedString>();

        if (options.ScanAscii)
            ScanAscii(data, options.MinLength, results);

        if (options.ScanUtf8)
            ScanUtf8(data, options.MinLength, results);

        if (options.ScanUtf16Le)
            ScanUtf16(data, options.MinLength, bigEndian: false, results);

        if (options.ScanUtf16Be)
            ScanUtf16(data, options.MinLength, bigEndian: true, results);

        // Deduplicate by (offset, encoding) and sort by offset.
        var seen    = new HashSet<(long, string)>();
        var ordered = results
            .Where(r => seen.Add((r.Offset, r.Encoding)))
            .OrderBy(r => r.Offset)
            .ToList();

        double total = ordered.Count;
        for (int i = 0; i < ordered.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report(i / total);
            yield return ordered[i];
            // Yield control every 500 items so the UI remains responsive.
            if (i % 500 == 0)
                await Task.Yield();
        }

        progress?.Report(1.0);
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
                int len = i - start;
                if (len >= minLen)
                {
                    var value = Encoding.ASCII.GetString(data, start, len).TrimEnd('\r', '\n');
                    if (!string.IsNullOrWhiteSpace(value))
                        results.Add(new ExtractedString(start, len, "ASCII", value));
                }
                start = -1;
            }
        }
    }

    private static void ScanUtf8(byte[] data, int minLen, List<ExtractedString> results)
    {
        // Use a sliding window: try to decode UTF-8 sequences, accumulate printable chars.
        var decoder = Encoding.UTF8.GetDecoder();
        var charBuf = new char[data.Length];
        int start   = -1;
        int i       = 0;

        while (i < data.Length)
        {
            // Determine sequence length from lead byte.
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
                int charCount = decoder.GetChars(data, i, seqLen, charBuf, 0);
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
        catch { /* malformed — skip */ }
    }

    private static int GetUtf8SequenceLength(byte b) => b switch
    {
        < 0x80 => 1,
        < 0xC0 => 0,  // continuation byte — invalid as lead
        < 0xE0 => 2,
        < 0xF0 => 3,
        < 0xF8 => 4,
        _      => 0
    };

    private static void ScanUtf16(byte[] data, int minLen, bool bigEndian, List<ExtractedString> results)
    {
        if (data.Length < 2) return;

        var encoding   = bigEndian ? Encoding.BigEndianUnicode : Encoding.Unicode;
        var encodingName = bigEndian ? "UTF-16 BE" : "UTF-16 LE";
        int start      = -1;

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
                int byteLen = i - start;
                int charLen = byteLen / 2;
                if (charLen >= minLen)
                {
                    var value = encoding.GetString(data, start, byteLen).TrimEnd('\r', '\n');
                    if (!string.IsNullOrWhiteSpace(value))
                        results.Add(new ExtractedString(start, byteLen, encodingName, value));
                }
                start = -1;
            }
        }

        if (start >= 0)
        {
            int byteLen = data.Length - start - (data.Length - start) % 2;
            int charLen = byteLen / 2;
            if (charLen >= minLen)
            {
                var value = encoding.GetString(data, start, byteLen).TrimEnd('\r', '\n');
                if (!string.IsNullOrWhiteSpace(value))
                    results.Add(new ExtractedString(start, byteLen, encodingName, value));
            }
        }
    }
}
