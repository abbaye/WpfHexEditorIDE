//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using WpfHexEditor.Core.Interfaces;

namespace WpfHexEditor.SDK.Contracts.Services;

/// <summary>
/// Provides access to the active HexEditor state for plugins.
/// Requires <c>AccessHexEditor</c> permission.
/// </summary>
public interface IHexEditorService
{
    /// <summary>Gets whether a HexEditor is currently active (file open).</summary>
    bool IsActive { get; }

    /// <summary>Gets the file path of the currently open file, or null if none.</summary>
    string? CurrentFilePath { get; }

    /// <summary>Gets the total size of the currently open file in bytes.</summary>
    long FileSize { get; }

    /// <summary>Gets the current scroll/caret position (byte offset from start).</summary>
    long CurrentOffset { get; }

    /// <summary>Gets the start of the current selection (inclusive byte offset), or -1 if no selection.</summary>
    long SelectionStart { get; }

    /// <summary>Gets the end of the current selection (exclusive byte offset), or -1 if no selection.</summary>
    long SelectionStop { get; }

    /// <summary>Gets the length of the current selection in bytes.</summary>
    long SelectionLength { get; }

    /// <summary>Gets the byte offset of the first byte currently visible in the hex viewport.</summary>
    long FirstVisibleByteOffset { get; }

    /// <summary>Gets the byte offset one past the last byte currently visible in the hex viewport.</summary>
    long LastVisibleByteOffset { get; }

    /// <summary>
    /// Raised when the user scrolls the hex viewport (mouse wheel or scrollbar drag).
    /// Raised on the UI thread.
    /// </summary>
    event EventHandler ViewportScrolled;

    /// <summary>
    /// Reads a block of bytes from the currently open file.
    /// </summary>
    /// <param name="offset">Start byte offset.</param>
    /// <param name="length">Number of bytes to read.</param>
    /// <returns>Byte array, or empty array if no file is open.</returns>
    byte[] ReadBytes(long offset, int length);

    /// <summary>
    /// Reads the currently selected bytes.
    /// </summary>
    /// <returns>Selected bytes, or empty array if no selection.</returns>
    byte[] GetSelectedBytes();

    /// <summary>
    /// Raised when the active selection changes.
    /// Raised on the UI thread.
    /// </summary>
    event EventHandler SelectionChanged;

    /// <summary>
    /// Searches for a hex pattern (e.g. "FF 0A") in the current file.
    /// </summary>
    /// <returns>List of matching byte offsets, or empty list if no match.</returns>
    IReadOnlyList<long> SearchHex(string hexPattern);

    /// <summary>
    /// Searches for a UTF-8 text string in the current file.
    /// </summary>
    /// <returns>List of matching byte offsets, or empty list if no match.</returns>
    IReadOnlyList<long> SearchText(string text);

    /// <summary>
    /// Writes bytes into the active file at the specified offset.
    /// </summary>
    /// <param name="offset">Start byte offset.</param>
    /// <param name="data">Bytes to write.</param>
    void WriteBytes(long offset, byte[] data);

    /// <summary>
    /// Raised when a new file is opened in the HexEditor.
    /// Raised on the UI thread.
    /// </summary>
    event EventHandler FileOpened;

    /// <summary>
    /// Raised when the HexEditor completes automatic format detection.
    /// Raised on the UI thread.
    /// </summary>
    event EventHandler<FormatDetectedArgs> FormatDetected;

    /// <summary>
    /// Programmatically sets the HexEditor selection range.
    /// Used by the StructureOverlay plugin to highlight parsed fields.
    /// </summary>
    /// <param name="start">Start byte offset (inclusive).</param>
    /// <param name="end">End byte offset (inclusive).</param>
    void SetSelection(long start, long end);

    /// <summary>
    /// Raised when the active HexEditor document tab changes (a different file becomes active).
    /// Plugins use this to disconnect/reconnect panels that have a 1:1 binding with the active editor.
    /// Raised on the UI thread.
    /// </summary>
    event EventHandler ActiveEditorChanged;

    /// <summary>
    /// Connects a <see cref="IParsedFieldsPanel"/> to the currently active HexEditor.
    /// Auto-wires all 5 bidirectional events (FieldSelected, RefreshRequested, FormatterChanged,
    /// FieldValueEdited, FormatCandidateSelected) via the HexEditor DependencyProperty.
    /// </summary>
    void ConnectParsedFieldsPanel(IParsedFieldsPanel panel);

    /// <summary>Disconnects the currently connected <see cref="IParsedFieldsPanel"/>.</summary>
    void DisconnectParsedFieldsPanel();
}
