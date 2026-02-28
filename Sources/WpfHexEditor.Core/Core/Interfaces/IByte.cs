//////////////////////////////////////////////
// Apache 2.0  - 2020-2021
// Base author  : ehsan69h
// Modified by  : Abbaye
//////////////////////////////////////////////

using System.Collections.Generic;
using System.Windows.Input;

namespace WpfHexaEditor.Core.Interfaces
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
