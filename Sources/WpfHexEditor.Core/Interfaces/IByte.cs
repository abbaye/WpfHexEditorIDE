// ==========================================================
// Project: WpfHexEditor.Core
// File: IByte.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic), ehsan69h, Abbaye
// Created: 2026-03-06
// Description:
//     Core interface defining the contract for all byte display units (8-bit,
//     16-bit, 32-bit). Provides a uniform API for the hex editor rendering
//     pipeline to handle single and multi-byte display cells.
//
// Architecture Notes:
//     Originally authored by ehsan69h / Abbaye. Implemented by Byte_8bit,
//     Byte_16bit, and Byte_32bit. Used by the HexEditor rendering pipeline
//     for polymorphic byte cell management. Contains WPF Key input dependency.
//
// ==========================================================

using System.Collections.Generic;
using System.Windows.Input;

namespace WpfHexEditor.Core.Interfaces
{
    public delegate void D_ByteListProp(List<byte> newValue, int index);

    interface IByte
    {
        public List<byte> Byte { get; set; }
        public List<byte> OriginByte { get; set; }

        public string GetText(DataVisualType type, DataVisualState state, ByteOrderType order);

        public D_ByteListProp del_ByteOnChange { get; set; }

        public bool IsEqual(byte[] bytes);

        public (ByteAction, bool) Update(DataVisualType type, Key _key, ByteOrderType byteOrder, ref KeyDownLabel _keyDownLabel);

        public void ChangeByteValue(byte newValue, long position);

        /// <summary>
        /// GetText() need to be called before
        /// </summary>
        public string Text { get; }

        /// <summary>
        /// GetText() need to be called before
        /// </summary>
        public long LongText { get; }

    }
}
