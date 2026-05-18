//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using WpfHexEditor.Core.CharacterTable;

namespace WpfHexEditor.App.BinaryAnalysis.Services;

/// <summary>
/// Adapts a <see cref="TblStream"/> to the <see cref="ITblDecodeTable"/> contract
/// consumed by <see cref="StringExtractor"/>. Maps single-byte keys to printable chars;
/// multi-byte entries are intentionally excluded (MTE handling is out of scope for string extraction).
/// </summary>
public sealed class TblDecodeTableAdapter : ITblDecodeTable
{
    private readonly Dictionary<byte, char> _map;

    public TblDecodeTableAdapter(TblStream tbl)
    {
        _map = new Dictionary<byte, char>(256);
        foreach (var entry in tbl.GetAllEntries())
        {
            // Only single-byte keys (2 hex chars)
            if (entry.Entry.Length != 2) continue;
            if (!byte.TryParse(entry.Entry, System.Globalization.NumberStyles.HexNumber, null, out byte b)) continue;
            if (string.IsNullOrEmpty(entry.Value) || entry.Value.Length != 1) continue;
            char ch = entry.Value[0];
            if (ch >= 0x20 && ch != 0x7F)
                _map[b] = ch;
        }
    }

    /// <inheritdoc/>
    public bool TryDecode(byte b, out char ch) => _map.TryGetValue(b, out ch);

    /// <summary>Number of mapped single-byte entries.</summary>
    public int MappedCount => _map.Count;
}
