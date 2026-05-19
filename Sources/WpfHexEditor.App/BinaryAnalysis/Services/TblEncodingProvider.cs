//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using WpfHexEditor.Core.Bytes;
using WpfHexEditor.Core.CharacterTable;

namespace WpfHexEditor.App.BinaryAnalysis.Services;

/// <summary>
/// Adapts a <see cref="TblStream"/> to <see cref="ITblDecodeTable"/> for string extraction.
/// Mirrors <c>TblStream.ToTblString()</c> exactly:
///   - Greedy longest-match (8 → 1 bytes); DTE (2-byte) and MTE (3-8 byte) honoured.
///   - EndBlock (/XX) and EndLine (*XX) flush the current run (not included in output).
///   - Unmapped bytes break the current run.
/// </summary>
public sealed class TblDecodeTableAdapter : ITblDecodeTable
{
    // Printable entries: hex-key (uppercase) → mapped text + byte-width.
    private readonly Dictionary<string, (string text, int width)> _map;

    // End-markers stored as raw hex keys (without the /  or * prefix).
    private readonly HashSet<string> _endMarkers;

    // Maximum key length in bytes (drives greedy scan depth, capped at 8 like TblStream).
    private readonly int _maxKeyBytes;

    public TblDecodeTableAdapter(TblStream tbl)
    {
        _map        = new Dictionary<string, (string, int)>(tbl.Count, StringComparer.OrdinalIgnoreCase);
        _endMarkers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in tbl.GetAllEntries())
        {
            if (entry.Type is DteType.EndBlock or DteType.EndLine)
            {
                // Entry key has /XX or *XX prefix — strip the leading symbol to get raw hex.
                var raw = entry.Entry.Length > 1 ? entry.Entry[1..] : string.Empty;
                if (raw.Length > 0 && raw.Length % 2 == 0)
                    _endMarkers.Add(raw.ToUpperInvariant());
                continue;
            }

            if (entry.Type == DteType.Invalid) continue;
            if (string.IsNullOrEmpty(entry.Value)) continue;

            var key = entry.Entry; // already uppercase (TblStream normalises on load)
            if (key.Length == 0 || key.Length % 2 != 0) continue;

            int byteLen = key.Length / 2;
            _map[key] = (entry.Value, byteLen);
            if (byteLen > _maxKeyBytes) _maxKeyBytes = byteLen;
        }

        if (_maxKeyBytes == 0) _maxKeyBytes = 1;
    }

    /// <inheritdoc/>
    public bool TryMatch(ReadOnlySpan<byte> data, int offset, out int bytesConsumed, out string text, out int byteWidth)
    {
        int maxLen = Math.Min(_maxKeyBytes, data.Length - offset);

        for (int len = maxLen; len >= 1; len--)
        {
            var key = BuildHexKey(data, offset, len);
            if (_map.TryGetValue(key, out var match))
            {
                bytesConsumed = len;
                text          = match.text;
                byteWidth     = match.width;
                return true;
            }
        }

        bytesConsumed = 0;
        text          = string.Empty;
        byteWidth     = 0;
        return false;
    }

    /// <inheritdoc/>
    public bool IsEndMarker(ReadOnlySpan<byte> data, int offset, out int markerBytes)
    {
        // End-markers can be multi-byte too — try longest first.
        int maxLen = Math.Min(_maxKeyBytes, data.Length - offset);
        for (int len = maxLen; len >= 1; len--)
        {
            var key = BuildHexKey(data, offset, len);
            if (_endMarkers.Contains(key))
            {
                markerBytes = len;
                return true;
            }
        }
        markerBytes = 0;
        return false;
    }

    private static string BuildHexKey(ReadOnlySpan<byte> data, int offset, int len)
    {
        var chars = new char[len * 2];
        for (int i = 0; i < len; i++)
        {
            byte b = data[offset + i];
            chars[i * 2]     = ByteConverters.ByteToHexChar(b >> 4);
            chars[i * 2 + 1] = ByteConverters.ByteToHexChar(b & 0x0F);
        }
        return new string(chars);
    }

    /// <summary>Number of printable mapped entries (excludes end-markers).</summary>
    public int MappedCount => _map.Count;

    /// <summary>Number of end-marker entries (EndBlock + EndLine).</summary>
    public int EndMarkerCount => _endMarkers.Count;
}
