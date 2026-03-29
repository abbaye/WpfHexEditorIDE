// ==========================================================
// Project: WpfHexEditor.Core
// File: BookMark.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     Represents a user-defined bookmark at a specific byte offset in the
//     file, used for navigation and annotation within the hex editor.
//     Supports named bookmarks with scroll marker types.
//
// Architecture Notes:
//     Pure domain model — no WPF dependencies. Owned by the hex editor's
//     bookmark collection and consumed by the UI layer for scroll indicator
//     and navigation rendering.
//
// ==========================================================

using WpfHexEditor.Core.Bytes;

namespace WpfHexEditor.Core
{
    /// <summary>
    /// BookMark used in hexeditor
    /// </summary>
    public class BookMark
    {
        public ScrollMarker Marker { get; set; } = ScrollMarker.Nothing;
        public long BytePositionInStream { get; set; }
        public string Description { get; set; } = string.Empty;

        public BookMark() { }

        public BookMark(string description, long position)
        {
            BytePositionInStream = position;
            Description = description;
        }

        public BookMark(string description, long position, ScrollMarker marker)
        {
            BytePositionInStream = position;
            Description = description;
            Marker = marker;
        }

        /// <summary>
        /// String representation
        /// </summary>
        public override string ToString() => $"({ByteConverters.LongToHex(BytePositionInStream)}h){Description}";
    }
}
