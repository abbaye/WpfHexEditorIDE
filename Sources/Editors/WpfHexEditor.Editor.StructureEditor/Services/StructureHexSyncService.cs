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

    /// <summary>Parses a hex string (e.g. "DE AD BE EF" or "DEADBEEF") to bytes; returns null on parse failure.</summary>
    public static byte[]? TryParseHex(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return null;
        var clean = hex.Replace(" ", "").Replace("-", "").Replace("0x", "", StringComparison.OrdinalIgnoreCase);
        if (clean.Length % 2 != 0) return null;
        var bytes = new byte[clean.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            if (!byte.TryParse(clean.AsSpan(i * 2, 2),
                System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out bytes[i]))
                return null;
        }
        return bytes;
    }
}
