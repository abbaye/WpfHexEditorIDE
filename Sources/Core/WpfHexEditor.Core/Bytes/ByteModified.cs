// ==========================================================
// Project: WpfHexEditor.Core
// File: ByteModified.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     Represents a single modified byte in the editing session, recording
//     the new value, the action type (modify/insert/delete), the stream
//     position, and the undo length needed for reversal.
//
// Architecture Notes:
//     Implements IByteModified interface. Used internally by EditsManager
//     and UndoRedoManager. Pure domain model — no WPF dependencies.
//
// ==========================================================

using WpfHexEditor.Core.Interfaces;

namespace WpfHexEditor.Core.Bytes
{
    public class ByteModified : IByteModified
    {
        #region Constructor

        /// <summary>
        /// Default contructor
        /// </summary>
        public ByteModified() { }

        /// <summary>
        /// complete contructor
        /// </summary>
        public ByteModified(byte? val, ByteAction action, long bytePositionInStream, long undoLength)
        {
            Byte = val;
            Action = action;
            BytePositionInStream = bytePositionInStream;
            Length = undoLength;
        }

        #endregion constructor

        #region properties

        /// <summary>
        /// Byte mofidied
        /// </summary>
        public byte? Byte { get; set; }

        /// <summary>
        /// Action have made in this byte
        /// </summary>
        public ByteAction Action { get; set; } = ByteAction.Nothing;

        /// <summary>
        /// Get of Set te position in file
        /// </summary>
        public long BytePositionInStream { get; set; } = -1;

        /// <summary>
        /// Number of byte to undo when this byte is reach
        /// </summary>
        public long Length { get; set; } = 1;

        #endregion properties

        #region Methods

        /// <summary>
        /// Check if the object is valid and data can be used for action
        /// </summary>
        public bool IsValid => BytePositionInStream > -1 && Action != ByteAction.Nothing && Byte is not null;

        /// <summary>
        /// String representation of byte
        /// </summary>
        public override string ToString() =>
            $"ByteModified - Action:{Action} Position:{BytePositionInStream} Byte:{Byte}";

        /// <summary>
        /// Clear object
        /// </summary>
        public void Clear()
        {
            Byte = null;
            Action = ByteAction.Nothing;
            BytePositionInStream = -1;
            Length = 1;
        }

        /// <summary>
        /// Copy Current instance to another
        /// </summary>
        public ByteModified GetCopy() => new()
        {
            Action = Action,
            Byte = Byte,
            Length = Length,
            BytePositionInStream = BytePositionInStream
        };

        /// <summary>
        /// Get if bytemodified is valid
        /// </summary>
        public static bool CheckIsValid(ByteModified byteModified) => byteModified is not null && byteModified.IsValid;

        #endregion Methods
    }
}
