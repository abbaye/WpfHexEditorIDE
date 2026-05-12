// ==========================================================
// Project: WpfHexEditor.Editor.StructureEditor
// File: Services/StructureHexSyncService.cs
// Description:
//     Bridges field edits in the BinaryPreview DataGrid back to the active
//     HexEditor. Holds the in-memory binary buffer and raises FieldEdited
//     events that the host (StructureEditor control / MainWindow) wires to
//     the HexEditor's ByteProvider.
// Architecture: pure C# event-source — no WPF, no IDEEventBus dependency.
//               Singleton-friendly, but constructor-injection-friendly too.
// ==========================================================

namespace WpfHexEditor.Editor.StructureEditor.Services;

/// <summary>
/// Event payload describing a field-level byte edit issued by the
/// BinaryPreview panel.
/// </summary>
public sealed class StructureFieldEditedEventArgs : EventArgs
{
    public required long   Offset    { get; init; }
    public required byte[] NewBytes  { get; init; }
    public required string FieldName { get; init; }
}

/// <summary>Contract for bidirectional sync between StructureEditor fields and a HexEditor.</summary>
public interface IStructureHexSyncService
{
    /// <summary>Raised after a successful field edit. Host wires to the active HexEditor.</summary>
    event EventHandler<StructureFieldEditedEventArgs>? FieldEdited;

    /// <summary>Returns the current in-memory binary buffer (or null if none loaded).</summary>
    byte[]? GetBytes();

    /// <summary>Sets the binary buffer (called by BinaryPreviewViewModel.LoadBinaryAsync).</summary>
    void SetBytes(byte[] bytes);

    /// <summary>Writes new bytes at <paramref name="offset"/> and notifies subscribers.</summary>
    bool WriteField(long offset, byte[] newBytes, string fieldName);
}

/// <summary>Default in-memory implementation.</summary>
public sealed class StructureHexSyncService : IStructureHexSyncService
{
    private byte[]? _bytes;

    public event EventHandler<StructureFieldEditedEventArgs>? FieldEdited;

    public byte[]? GetBytes() => _bytes;

    public void SetBytes(byte[] bytes) => _bytes = bytes;

    public bool WriteField(long offset, byte[] newBytes, string fieldName)
    {
        if (_bytes is null) return false;
        if (offset < 0 || offset + newBytes.Length > _bytes.LongLength) return false;

        Buffer.BlockCopy(newBytes, 0, _bytes, (int)offset, newBytes.Length);

        FieldEdited?.Invoke(this, new StructureFieldEditedEventArgs
        {
            Offset    = offset,
            NewBytes  = newBytes,
            FieldName = fieldName,
        });
        return true;
    }

    /// <summary>
    /// Parses a hex string (e.g. "DE AD BE EF", "DE-AD-BE-EF", "0xDEADBEEF",
    /// "DEADBEEF") to bytes; returns null on parse failure. Single-pass — no
    /// intermediate string allocation.
    /// </summary>
    public static byte[]? TryParseHex(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return null;

        // Skip leading "0x" / "0X".
        int start = (hex.Length >= 2 && hex[0] == '0' && (hex[1] == 'x' || hex[1] == 'X')) ? 2 : 0;

        // Count nibble chars first to avoid a List<byte> allocation.
        int nibbles = 0;
        for (int i = start; i < hex.Length; i++)
            if (!char.IsWhiteSpace(hex[i]) && hex[i] != '-') nibbles++;
        if (nibbles == 0 || (nibbles & 1) != 0) return null;

        var bytes = new byte[nibbles / 2];
        int b = 0, accum = 0, halfNibble = 0;
        for (int i = start; i < hex.Length; i++)
        {
            var c = hex[i];
            if (char.IsWhiteSpace(c) || c == '-') continue;
            int v = FromHex(c);
            if (v < 0) return null;
            accum = (accum << 4) | v;
            if (++halfNibble == 2)
            {
                bytes[b++] = (byte)accum;
                accum = 0;
                halfNibble = 0;
            }
        }
        return bytes;
    }

    private static int FromHex(char c) =>
        c is >= '0' and <= '9' ? c - '0'      :
        c is >= 'a' and <= 'f' ? c - 'a' + 10 :
        c is >= 'A' and <= 'F' ? c - 'A' + 10 : -1;
}
