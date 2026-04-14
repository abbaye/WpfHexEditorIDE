// ==========================================================
// Project: WpfHexEditor.Core
// File: IByteModified.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     Interface defining the contract for modified byte records in the editing
//     session, specifying the action type, position, new value, and length
//     required for undo/redo and changeset operations.
//
// Architecture Notes:
//     Implemented by ByteModified. Consumed by EditsManager and UndoRedoManager.
//     Pure domain interface — no WPF dependencies.
//
// ==========================================================

using WpfHexEditor.Core.Bytes;

namespace WpfHexEditor.Core.Interfaces
{
    public interface IByteModified
    {
        //Properties
        ByteAction Action { get; set; }

        byte? Byte { get; set; }
        long BytePositionInStream { get; set; }
        bool IsValid { get; }
        long Length { get; set; }

        //Methods
        void Clear();

        ByteModified GetCopy();
        string ToString();
    }
}
