// ==========================================================
// Project: WpfHexEditor.SDK
// File: IHexEditorService.cs
// Author: Auto
// Created: 2026-03-06
// Description:
//     Plugin-facing service for accessing the active HexEditor content,
//     selection, offset, and byte data — without exposing WPF internals.
//
// Architecture Notes:
//     Implemented by HexEditorServiceImpl in App/Services, wrapping ActiveHexEditor.
//     Read-only by default; write operations require AccessHexEditor permission.
//     Requires PluginPermission.AccessHexEditor to be granted.
//
// ==========================================================

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
    /// Raised when a new file is opened in the HexEditor.
    /// Raised on the UI thread.
    /// </summary>
    event EventHandler FileOpened;
}
