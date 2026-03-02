//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using WpfHexEditor.Core;

namespace WpfHexEditor.Core.Events
{
    /// <summary>
    /// Event args for byte modification events
    /// </summary>
    public class ByteModifiedEventArgs : EventArgs
    {
        /// <summary>
        /// Position of the modified byte (virtual position)
        /// </summary>
        public long Position { get; set; }

        /// <summary>
        /// Original value before modification
        /// </summary>
        public byte OldValue { get; set; }

        /// <summary>
        /// New value after modification
        /// </summary>
        public byte NewValue { get; set; }

        /// <summary>
        /// Type of byte action (Modified, Added, Deleted)
        /// </summary>
        public ByteAction Action { get; set; }

        public ByteModifiedEventArgs(long position, byte oldValue, byte newValue, ByteAction action)
        {
            Position = position;
            OldValue = oldValue;
            NewValue = newValue;
            Action = action;
        }
    }

    /// <summary>
    /// Event args for position change events
    /// </summary>
    public class PositionChangedEventArgs : EventArgs
    {
        /// <summary>
        /// New cursor position (virtual position)
        /// </summary>
        public long Position { get; set; }

        public PositionChangedEventArgs(long position)
        {
            Position = position;
        }
    }

    /// <summary>
    /// Event args for selection change events
    /// </summary>
    public class HexSelectionChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Selection start position (virtual)
        /// </summary>
        public long SelectionStart { get; set; }

        /// <summary>
        /// Selection stop position (virtual)
        /// </summary>
        public long SelectionStop { get; set; }

        /// <summary>
        /// Selection length in bytes
        /// </summary>
        public long SelectionLength { get; set; }

        public HexSelectionChangedEventArgs(long selectionStart, long selectionStop, long selectionLength)
        {
            SelectionStart = selectionStart;
            SelectionStop = selectionStop;
            SelectionLength = selectionLength;
        }
    }

    /// <summary>
    /// V1 compatible: Event args for byte interaction events (click, scroll)
    /// </summary>
    public class ByteEventArgs : EventArgs
    {
        /// <summary>
        /// Byte position (virtual)
        /// </summary>
        public long BytePositionInStream { get; set; }

        /// <summary>
        /// Byte value at position
        /// </summary>
        public byte? Byte { get; set; }

        /// <summary>
        /// Line number
        /// </summary>
        public long LineNumber { get; set; }

        public ByteEventArgs(long position, byte? byteValue = null, long lineNumber = 0)
        {
            BytePositionInStream = position;
            Byte = byteValue;
            LineNumber = lineNumber;
        }
    }
}
