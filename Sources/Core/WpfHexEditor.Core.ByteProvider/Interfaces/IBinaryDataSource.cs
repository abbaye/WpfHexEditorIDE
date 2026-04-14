// ==========================================================
// Project: WpfHexEditor.Core
// File: IBinaryDataSource.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-26
// Description:
//     Universal byte provider abstraction. Any editor or plugin that
//     can supply binary data implements this interface to enable
//     format detection and parsed field display.
//
// Architecture Notes:
//     Adapter pattern — thin wrappers per editor (HexEditor, DiffViewer, etc.)
//     sit between this interface and the editor's native I/O surface.
//     FormatParsingService consumes IBinaryDataSource exclusively; it never
//     touches editor-specific types like Stream or ByteProvider.
// ==========================================================

using System;

namespace WpfHexEditor.Core.Interfaces
{
    /// <summary>
    /// Universal byte provider for format detection and field parsing.
    /// Implemented by editor-specific adapters (HexEditor, DiffViewer, etc.).
    /// </summary>
    public interface IBinaryDataSource
    {
        /// <summary>Full path of the backing file, or null for in-memory buffers.</summary>
        string? FilePath { get; }

        /// <summary>Total byte count available from this source.</summary>
        long Length { get; }

        /// <summary>True when the source does not support writes.</summary>
        bool IsReadOnly { get; }

        /// <summary>
        /// Read <paramref name="length"/> bytes starting at <paramref name="offset"/>.
        /// Returns an array of exactly <paramref name="length"/> bytes,
        /// or a shorter/empty array if offset+length exceeds <see cref="Length"/>.
        /// </summary>
        byte[] ReadBytes(long offset, int length);

        /// <summary>
        /// Write <paramref name="data"/> starting at <paramref name="offset"/>.
        /// Throws <see cref="InvalidOperationException"/> if <see cref="IsReadOnly"/> is true.
        /// </summary>
        void WriteBytes(long offset, byte[] data);

        /// <summary>
        /// Raised when the underlying data changes (byte edit, undo, redo, etc.).
        /// Consumers should throttle refresh on this event.
        /// </summary>
        event EventHandler? DataChanged;
    }
}
