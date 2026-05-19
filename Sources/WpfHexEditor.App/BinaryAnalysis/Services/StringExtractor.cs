//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Text;
using WpfHexEditor.Core.Bytes;

namespace WpfHexEditor.App.BinaryAnalysis.Services;

/// <summary>Encoding used for a single extracted string run.</summary>
public enum StringEncoding
{
    Ascii,
    Utf16Le,
    Utf16Be,
    Utf8,
    Latin1,
    Ebcdic,
    EbcdicNoSpec,
    Tbl,      // single-byte TBL entries only
    TblDte,   // run contains at least one 2-byte DTE entry
    TblMte,   // run contains at least one 3–8-byte MTE entry
}

/// <summary>Scan result for one extracted string run.</summary>
/// <param name="RawHex">Space-separated uppercase hex bytes for the matched region (e.g. "4A 6F 6E").</param>
public sealed record StringRun(long Offset, int Length, StringEncoding Encoding, string Value, string RawHex)
{
    /// <summary>Count of distinct characters in Value — cached to avoid per-filter-pass allocation.</summary>
    public int UniqueCharCount { get; } = Value.Distinct().Count();
}

/// <summary>
/// Decode contract used by <see cref="StringExtractor"/> for TBL-mode scanning.
/// Mirrors <c>TblStream.ToTblString()</c>: greedy longest-match, DTE/MTE honoured,
/// EndBlock/EndLine flush the current run.
/// </summary>
public interface ITblDecodeTable
{
    /// <summary>
    /// Try to match the longest printable (non-control) entry starting at <paramref name="offset"/>.
    /// Returns true when a mapped sequence is found; <paramref name="byteWidth"/> indicates
    /// how many bytes were consumed (1 = Ascii, 2 = DTE, ≥3 = MTE).
    /// </summary>
    bool TryMatch(ReadOnlySpan<byte> data, int offset, out int bytesConsumed, out string text, out int byteWidth);

    /// <summary>
    /// Returns true when the bytes at <paramref name="offset"/> are an EndBlock or EndLine marker.
    /// The caller should flush the current run and skip <paramref name="markerBytes"/> bytes.
    /// </summary>
    bool IsEndMarker(ReadOnlySpan<byte> data, int offset, out int markerBytes);
}

/// <summary>Stateless service: extracts printable string runs from a byte buffer.</summary>
public static class StringExtractor
{
    private const byte MinPrintableAscii = 0x20;
    private const byte MaxPrintableAscii = 0x7E;

    // EBCDIC printable range heuristic: 0x40–0xFE minus control codes
    private static readonly HashSet<byte> _ebcdicPrintable = BuildEbcdicPrintable();
    private static readonly HashSet<byte> _ebcdicNoSpecPrintable = BuildEbcdicNoSpecPrintable();

    // IBM037 (EBCDIC) decoder
    private static readonly Encoding _ebcdicEncoding;
    private static readonly Encoding _latin1Encoding;

    static StringExtractor()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        _ebcdicEncoding    = Encoding.GetEncoding(37);   // IBM037 EBCDIC
        _latin1Encoding    = Encoding.GetEncoding(1252); // Windows-1252 (Latin-1 superset)
    }

    /// <summary>
    /// Scans <paramref name="data"/> for printable string runs of at least <paramref name="minLength"/> characters.
    /// </summary>
    /// <param name="data">Raw byte buffer to scan.</param>
    /// <param name="minLength">Minimum run length in characters.</param>
    /// <param name="encodings">Encodings to include. Defaults to ASCII + UTF-16 LE.</param>
    /// <param name="tbl">Optional TBL decode table for <see cref="StringEncoding.Tbl"/> mode.</param>
    public static List<StringRun> Extract(
        ReadOnlySpan<byte> data,
        int minLength = 4,
        IReadOnlySet<StringEncoding>? encodings = null,
        ITblDecodeTable? tbl = null)
    {
        var active = encodings ?? DefaultEncodings;
        var results = new List<StringRun>();

        if (active.Contains(StringEncoding.Ascii))        ExtractSingleByte(data, minLength, StringEncoding.Ascii,        IsAsciiPrintable,        results);
        if (active.Contains(StringEncoding.Utf16Le))      ExtractUtf16(data, minLength, StringEncoding.Utf16Le,           bigEndian: false,        results);
        if (active.Contains(StringEncoding.Utf16Be))      ExtractUtf16(data, minLength, StringEncoding.Utf16Be,           bigEndian: true,         results);
        if (active.Contains(StringEncoding.Utf8))         ExtractUtf8(data, minLength, results);
        if (active.Contains(StringEncoding.Latin1))       ExtractSingleByte(data, minLength, StringEncoding.Latin1,       IsLatin1Printable,       results);
        if (active.Contains(StringEncoding.Ebcdic))       ExtractSingleByte(data, minLength, StringEncoding.Ebcdic,       IsEbcdicPrintable,       results);
        if (active.Contains(StringEncoding.EbcdicNoSpec)) ExtractSingleByte(data, minLength, StringEncoding.EbcdicNoSpec, IsEbcdicNoSpecPrintable, results);
        if (active.Contains(StringEncoding.Tbl) && tbl is not null) ExtractTbl(data, minLength, tbl, results);

        results.Sort(static (a, b) => a.Offset.CompareTo(b.Offset));
        RemoveOverlaps(results);
        return results;
    }

    // ── Overlap removal ───────────────────────────────────────────────────────

    // Higher value = higher priority; TBL wins over ASCII over Latin1 etc.
    private static int EncodingPriority(StringEncoding e) => e switch
    {
        StringEncoding.TblMte        => 10,
        StringEncoding.TblDte        => 9,
        StringEncoding.Tbl           => 8,
        StringEncoding.Utf8          => 7,
        StringEncoding.Utf16Le       => 6,
        StringEncoding.Utf16Be       => 5,
        StringEncoding.Latin1        => 4,
        StringEncoding.Ascii         => 3,
        StringEncoding.Ebcdic        => 2,
        StringEncoding.EbcdicNoSpec  => 1,
        _                            => 0,
    };

    /// <summary>
    /// Remove runs whose byte ranges overlap a retained run of equal or higher priority.
    /// Input must be sorted by offset. Modifies the list in-place.
    /// </summary>
    private static void RemoveOverlaps(List<StringRun> runs)
    {
        if (runs.Count < 2) return;

        int writeIdx  = 0;
        long highWater = 0; // end offset (exclusive) of the last retained run

        for (int i = 0; i < runs.Count; i++)
        {
            var cur = runs[i];
            long curEnd = cur.Offset + cur.Length;

            if (cur.Offset >= highWater)
            {
                runs[writeIdx++] = cur;
                highWater = curEnd;
                continue;
            }

            var prev = runs[writeIdx - 1];
            if (EncodingPriority(cur.Encoding) > EncodingPriority(prev.Encoding))
            {
                runs[writeIdx - 1] = cur;
                highWater = Math.Max(highWater, curEnd);
            }
        }

        runs.RemoveRange(writeIdx, runs.Count - writeIdx);
    }

    // ── Default encoding set (backward compat) ────────────────────────────────

    private static readonly IReadOnlySet<StringEncoding> DefaultEncodings =
        new HashSet<StringEncoding> { StringEncoding.Ascii, StringEncoding.Utf16Le };

    // ── Printable predicates ──────────────────────────────────────────────────

    private static bool IsAsciiPrintable(byte b)        => b >= MinPrintableAscii && b <= MaxPrintableAscii;
    private static bool IsLatin1Printable(byte b)        => b >= 0x20 && b != 0x7F && !(b >= 0x80 && b <= 0x9F);
    private static bool IsEbcdicPrintable(byte b)        => _ebcdicPrintable.Contains(b);
    private static bool IsEbcdicNoSpecPrintable(byte b)  => _ebcdicNoSpecPrintable.Contains(b);

    // ── Single-byte extractor (generic) ──────────────────────────────────────

    private static void ExtractSingleByte(
        ReadOnlySpan<byte> data,
        int minLength,
        StringEncoding encoding,
        Func<byte, bool> isPrintable,
        List<StringRun> results)
    {
        int start = -1;
        for (int i = 0; i <= data.Length; i++)
        {
            bool printable = i < data.Length && isPrintable(data[i]);
            if (printable)
            {
                if (start < 0) start = i;
            }
            else if (start >= 0)
            {
                int len = i - start;
                if (len >= minLength)
                    results.Add(new StringRun(start, len, encoding, DecodeBytes(data.Slice(start, len), encoding), ToRawHex(data, start, len)));
                start = -1;
            }
        }
    }

    // ── UTF-16 extractor (LE or BE) ───────────────────────────────────────────

    private static void ExtractUtf16(
        ReadOnlySpan<byte> data,
        int minLength,
        StringEncoding encoding,
        bool bigEndian,
        List<StringRun> results)
    {
        int start = -1, charCount = 0;
        for (int i = 0; i + 1 < data.Length; i += 2)
        {
            ushort ch = bigEndian
                ? (ushort)((data[i] << 8) | data[i + 1])
                : (ushort)(data[i] | (data[i + 1] << 8));
            bool printable = ch >= MinPrintableAscii && ch <= MaxPrintableAscii;
            if (printable)
            {
                if (start < 0) { start = i; charCount = 0; }
                charCount++;
            }
            else if (start >= 0)
            {
                if (charCount >= minLength)
                {
                    int byteLen = charCount * 2;
                    var enc = bigEndian ? Encoding.BigEndianUnicode : Encoding.Unicode;
                    results.Add(new StringRun(start, byteLen, encoding, enc.GetString(data.Slice(start, byteLen)), ToRawHex(data, start, byteLen)));
                }
                start = -1;
            }
        }
        if (start >= 0 && charCount >= minLength)
        {
            int byteLen = charCount * 2;
            var enc = bigEndian ? Encoding.BigEndianUnicode : Encoding.Unicode;
            results.Add(new StringRun(start, byteLen, encoding, enc.GetString(data.Slice(start, byteLen)), ToRawHex(data, start, byteLen)));
        }
    }

    // ── UTF-8 extractor ───────────────────────────────────────────────────────

    private static void ExtractUtf8(ReadOnlySpan<byte> data, int minLength, List<StringRun> results)
    {
        int start = -1, charCount = 0;
        int i = 0;
        while (i < data.Length)
        {
            byte b = data[i];
            int seqLen = Utf8SequenceLength(b);
            if (seqLen > 0 && i + seqLen <= data.Length && IsValidUtf8Sequence(data, i, seqLen))
            {
                if (start < 0) { start = i; charCount = 0; }
                charCount++;
                i += seqLen;
            }
            else
            {
                FlushUtf8Run(data, start, i, charCount, minLength, results);
                start = -1; charCount = 0;
                i++;
            }
        }
        FlushUtf8Run(data, start, data.Length, charCount, minLength, results);
    }

    private static void FlushUtf8Run(ReadOnlySpan<byte> data, int start, int end, int charCount, int minLength, List<StringRun> results)
    {
        if (start < 0 || charCount < minLength) return;
        int len = end - start;
        results.Add(new StringRun(start, len, StringEncoding.Utf8, Encoding.UTF8.GetString(data.Slice(start, len)), ToRawHex(data, start, len)));
    }

    private static int Utf8SequenceLength(byte b) =>
        b < 0x80 && b >= 0x20 && b != 0x7F ? 1 :
        (b & 0xE0) == 0xC0 ? 2 :
        (b & 0xF0) == 0xE0 ? 3 :
        (b & 0xF8) == 0xF0 ? 4 : 0;

    private static bool IsValidUtf8Sequence(ReadOnlySpan<byte> data, int i, int len)
    {
        for (int j = 1; j < len; j++)
            if ((data[i + j] & 0xC0) != 0x80) return false;
        return true;
    }

    // ── TBL extractor ─────────────────────────────────────────────────────────

    private static void ExtractTbl(ReadOnlySpan<byte> data, int minLength, ITblDecodeTable tbl, List<StringRun> results)
    {
        int i = 0;
        int start = -1;
        int startByteEnd = 0;
        int maxByteWidth = 1;
        var sb = new StringBuilder();

        while (i < data.Length)
        {
            if (tbl.IsEndMarker(data, i, out int markerBytes))
            {
                FlushTbl(data, minLength, start, startByteEnd, maxByteWidth, sb, results);
                start = -1; startByteEnd = 0; maxByteWidth = 1; sb.Clear();
                i += markerBytes;
                continue;
            }

            if (tbl.TryMatch(data, i, out int consumed, out string text, out int byteWidth))
            {
                if (start < 0) start = i;
                if (byteWidth > maxByteWidth) maxByteWidth = byteWidth;
                sb.Append(text);
                i += consumed;
                startByteEnd = i;
            }
            else
            {
                FlushTbl(data, minLength, start, startByteEnd, maxByteWidth, sb, results);
                start = -1; startByteEnd = 0; maxByteWidth = 1; sb.Clear();
                i++;
            }
        }
        FlushTbl(data, minLength, start, startByteEnd, maxByteWidth, sb, results);
    }

    private static void FlushTbl(ReadOnlySpan<byte> data, int minLength, int start, int startByteEnd, int maxByteWidth, StringBuilder sb, List<StringRun> results)
    {
        if (start < 0 || sb.Length < minLength) return;
        var enc = maxByteWidth >= 3 ? StringEncoding.TblMte
                : maxByteWidth == 2 ? StringEncoding.TblDte
                : StringEncoding.Tbl;
        int len = startByteEnd - start;
        results.Add(new StringRun(start, len, enc, sb.ToString(), ToRawHex(data, start, len)));
    }

    // ── Decode helpers ────────────────────────────────────────────────────────

    private static string DecodeBytes(ReadOnlySpan<byte> bytes, StringEncoding enc) =>
        enc switch
        {
            StringEncoding.Ebcdic       => _ebcdicEncoding.GetString(bytes),
            StringEncoding.EbcdicNoSpec => _ebcdicEncoding.GetString(bytes),
            StringEncoding.Latin1       => _latin1Encoding.GetString(bytes),
            _                           => Encoding.ASCII.GetString(bytes),
        };

    private static string ToRawHex(ReadOnlySpan<byte> data, int offset, int len)
    {
        if (len <= 0) return string.Empty;
        // "XX XX XX" — 3 chars per byte minus trailing space
        var chars = new char[len * 3 - 1];
        for (int i = 0; i < len; i++)
        {
            byte b = data[offset + i];
            chars[i * 3]     = ByteConverters.ByteToHexChar(b >> 4);
            chars[i * 3 + 1] = ByteConverters.ByteToHexChar(b & 0x0F);
            if (i < len - 1) chars[i * 3 + 2] = ' ';
        }
        return new string(chars);
    }

    // ── EBCDIC printable sets ─────────────────────────────────────────────────

    private static HashSet<byte> BuildEbcdicPrintable()
    {
        // Printable IBM037 EBCDIC: space=0x40, punctuation, alpha, numeric
        var s = new HashSet<byte>();
        byte[] printableRanges =
        [
            0x40, // space
            0x4B, 0x4C, 0x4D, 0x4E, 0x4F, // . < ( + |
            0x50, 0x5A, 0x5B, 0x5C, 0x5D, 0x5E, 0x5F, // & ) * ; ^ -
            0x60, 0x61, 0x6A, 0x6B, 0x6C, 0x6D, 0x6E, 0x6F,
            0x79, 0x7A, 0x7B, 0x7C, 0x7D, 0x7E, 0x7F,
        ];
        foreach (var b in printableRanges) s.Add(b);
        // Alphabetic: A–I=0xC1–0xC9, J–R=0xD1–0xD9, S–Z=0xE2–0xE9
        for (byte b = 0xC1; b <= 0xC9; b++) s.Add(b);
        for (byte b = 0xD1; b <= 0xD9; b++) s.Add(b);
        for (byte b = 0xE2; b <= 0xE9; b++) s.Add(b);
        // lowercase a–i=0x81–0x89, j–r=0x91–0x99, s–z=0xA2–0xA9
        for (byte b = 0x81; b <= 0x89; b++) s.Add(b);
        for (byte b = 0x91; b <= 0x99; b++) s.Add(b);
        for (byte b = 0xA2; b <= 0xA9; b++) s.Add(b);
        // digits 0–9=0xF0–0xF9
        for (byte b = 0xF0; b <= 0xF9; b++) s.Add(b);
        return s;
    }

    private static HashSet<byte> BuildEbcdicNoSpecPrintable()
    {
        var s = BuildEbcdicPrintable();
        s.Add(0x40); // space only, remove punctuation
        byte[] specials = [0x4B,0x4C,0x4D,0x4E,0x4F,0x50,0x5A,0x5B,0x5C,0x5D,0x5E,0x5F,
                           0x60,0x61,0x6A,0x6B,0x6C,0x6D,0x6E,0x6F,0x79,0x7A,0x7B,0x7C,0x7D,0x7E,0x7F];
        foreach (var b in specials) s.Remove(b);
        return s;
    }
}
