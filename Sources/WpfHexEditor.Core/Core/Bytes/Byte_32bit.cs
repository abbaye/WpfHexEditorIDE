//////////////////////////////////////////////
// Apache 2.0  - 2020-2021
// Base author  : ehsan69h
// Modified by  : Abbaye
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using WpfHexaEditor.Core.Interfaces;

namespace WpfHexaEditor.Core.Bytes
{
    class Byte_32bit : IByte
    {
        public Byte_32bit(byte[] value) => OriginByte = new List<byte>(value);

        private List<byte> _originByte;

        public List<byte> Byte { get; set; }

        public List<byte> OriginByte
        {
            get => _originByte;
            set
            {
                _originByte = value;
                Byte = new List<byte>(value);
            }
        }
        public D_ByteListProp del_ByteOnChange { get; set; }

        public string Text { get; internal set; }

        public long LongText { get; internal set; }

        public string GetText(DataVisualType type, DataVisualState state, ByteOrderType order)
        {
            var text = "";
            var value = new byte[4];
            var sign_positive = true;
            var prefix = "";

            var byteValue = (order == ByteOrderType.HiLo)
                ? Byte.ToArray().AsEnumerable().Reverse().ToArray()
                : Byte.ToArray();
            var originValue = (order == ByteOrderType.HiLo)
                ? OriginByte.ToArray().AsEnumerable().Reverse().ToArray()
                : OriginByte.ToArray();
            var ByteInt = BitConverter.ToUInt32(byteValue, 0);
            var OriginInt = BitConverter.ToUInt32(originValue, 0);

            switch (state)
            {
                case DataVisualState.Default:
                    value = byteValue;
                    break;
                case DataVisualState.Origin:
                    value = originValue;
                    break;
                case DataVisualState.Changes:
                    if (ByteInt.CompareTo(OriginInt) < 0)
                    {
                        sign_positive = false;
                        value = BitConverter.GetBytes(OriginInt - ByteInt).Take(4).Reverse().ToArray();
                    }
                    else
                        value = BitConverter.GetBytes(ByteInt - OriginInt).Take(4).Reverse().ToArray();

                    break;
                case DataVisualState.ChangesPercent:
                    prefix = "%";
                    if (ByteInt.CompareTo(OriginInt) < 0)
                    {
                        sign_positive = false;
                        value = BitConverter.GetBytes((uint)(((ulong)OriginInt - ByteInt) * 100 / uint.MaxValue));
                    }
                    else
                        value = BitConverter.GetBytes((uint)(((ulong)ByteInt - OriginInt) * 100 / uint.MaxValue));

                    break;
                default:
                    goto case DataVisualState.Default;
            }

            if (state == DataVisualState.ChangesPercent)
                text = (sign_positive ? "" : "-") + prefix + BitConverter.ToUInt16(value, 0).ToString("d2");
            else
                switch (type)
                {
                    case DataVisualType.Hexadecimal:
                        var chArr = ByteConverters.ByteToHexCharArray(
                            value[0]).Concat(
                            ByteConverters.ByteToHexCharArray(value[1]).Concat(
                                ByteConverters.ByteToHexCharArray(value[2]).Concat(
                                    ByteConverters.ByteToHexCharArray(value[3])))).ToArray();
                        text = (sign_positive ? "" : "-") + prefix +
                            new string(chArr);
                        break;
                    case DataVisualType.Decimal:
                        text = (sign_positive ? "" : "-") + prefix +
                            BitConverter.ToUInt32(value.AsEnumerable().Reverse().ToArray(), 0).ToString("d10");
                        break;
                    case DataVisualType.Binary:
                        text = (sign_positive ? "" : "-") + prefix +
                            Convert.ToString(value[0], 2).PadLeft(8, '0')
                            + Convert.ToString(value[1], 2).PadLeft(8, '0')
                            + Convert.ToString(value[2], 2).PadLeft(8, '0')
                            + Convert.ToString(value[3], 2).PadLeft(8, '0');
                        break;
                }

            LongText = ByteConverters.HexLiteralToLong(text).position;
            Text = text;

            return text;
        }


        public bool IsEqual(byte[] bytes)
        {
            if (bytes == null || bytes.Length != 2) return false;
            if (Byte == null || Byte.Count != 2) return false;
            if (bytes[0] == Byte[0] && bytes[1] == Byte[1]) return true;

            return false;
        }
        public void ChangeByte(int index, byte value, ByteOrderType byteOrder = ByteOrderType.LoHi)
        {
            if (byteOrder == ByteOrderType.HiLo)
            {
                index = 3 - index;
            }
            Byte[index] = value;
            del_ByteOnChange?.Invoke(Byte, index);
        }

        public (ByteAction, bool) Update(DataVisualType type, Key _key, ByteOrderType byteOrder, ref KeyDownLabel _keyDownLabel)
        {
            var Action = ByteAction.Nothing;
            var isLastChar = false;

            switch (type)
            {
                case DataVisualType.Hexadecimal:

                    #region Edit hexadecimal value 

                    var key = KeyValidator.IsNumericKey(_key)
                        ? KeyValidator.GetDigitFromKey(_key).ToString()
                        : _key.ToString().ToLower();

                    //Update byte
                    var byteValueCharArray = byteOrder == ByteOrderType.LoHi
                        ? ByteConverters.ByteToHexCharArray(Byte[0]).Concat(
                            ByteConverters.ByteToHexCharArray(Byte[1]).Concat(
                                ByteConverters.ByteToHexCharArray(Byte[2]).Concat(
                                    ByteConverters.ByteToHexCharArray(Byte[3])))).ToArray()
                        : ByteConverters.ByteToHexCharArray(Byte[3]).Concat(
                            ByteConverters.ByteToHexCharArray(Byte[2]).Concat(
                                ByteConverters.ByteToHexCharArray(Byte[1]).Concat(
                                    ByteConverters.ByteToHexCharArray(Byte[0])))).ToArray();
                    switch (_keyDownLabel)
                    {
                        case KeyDownLabel.FirstChar:
                            byteValueCharArray[0] = key.ToCharArray()[0];
                            _keyDownLabel = KeyDownLabel.SecondChar;
                            Action = ByteAction.Modified;
                            ChangeByte(0, ByteConverters.HexToByte(byteValueCharArray[0] + byteValueCharArray[1].ToString())[0], byteOrder: byteOrder);
                            break;

                        case KeyDownLabel.SecondChar:
                            byteValueCharArray[1] = key.ToCharArray()[0];
                            _keyDownLabel = KeyDownLabel.ThirdChar;

                            Action = ByteAction.Modified;
                            ChangeByte(0, ByteConverters.HexToByte(byteValueCharArray[0] + byteValueCharArray[1].ToString())[0], byteOrder: byteOrder);
                            break;
                        case KeyDownLabel.ThirdChar:
                            byteValueCharArray[2] = key.ToCharArray()[0];
                            _keyDownLabel = KeyDownLabel.FourthChar;
                            Action = ByteAction.Modified;
                            ChangeByte(1, ByteConverters.HexToByte(byteValueCharArray[2] + byteValueCharArray[3].ToString())[0], byteOrder: byteOrder);
                            break;
                        case KeyDownLabel.FourthChar:
                            byteValueCharArray[3] = key.ToCharArray()[0];
                            _keyDownLabel = KeyDownLabel.FifthChar;

                            Action = ByteAction.Modified;
                            ChangeByte(1, ByteConverters.HexToByte(byteValueCharArray[2] + byteValueCharArray[3].ToString())[0], byteOrder: byteOrder);
                            break;

                        case KeyDownLabel.FifthChar:
                            byteValueCharArray[4] = key.ToCharArray()[0];
                            _keyDownLabel = KeyDownLabel.SixthChar;
                            Action = ByteAction.Modified;
                            ChangeByte(2, ByteConverters.HexToByte(byteValueCharArray[4] + byteValueCharArray[5].ToString())[0], byteOrder: byteOrder);
                            break;

                        case KeyDownLabel.SixthChar:
                            byteValueCharArray[5] = key.ToCharArray()[0];
                            _keyDownLabel = KeyDownLabel.SeventhChar;
                            Action = ByteAction.Modified;
                            ChangeByte(2, ByteConverters.HexToByte(byteValueCharArray[4] + byteValueCharArray[5].ToString())[0], byteOrder: byteOrder);
                            break;

                        case KeyDownLabel.SeventhChar:
                            byteValueCharArray[6] = key.ToCharArray()[0];
                            _keyDownLabel = KeyDownLabel.EighthChar;
                            Action = ByteAction.Modified;
                            ChangeByte(3, ByteConverters.HexToByte(byteValueCharArray[6] + byteValueCharArray[7].ToString())[0], byteOrder: byteOrder);
                            break;

                        case KeyDownLabel.EighthChar:
                            byteValueCharArray[7] = key.ToCharArray()[0];
                            _keyDownLabel = KeyDownLabel.NextPosition;
                            Action = ByteAction.Modified;
                            ChangeByte(3, ByteConverters.HexToByte(byteValueCharArray[6] + byteValueCharArray[7].ToString())[0], byteOrder: byteOrder);
                            isLastChar = true;
                            break;
                        case KeyDownLabel.NextPosition:
                            break;
                    }

                    #endregion

                    break;
                case DataVisualType.Decimal:

                    #region Edit decimal value 

                    if (!KeyValidator.IsNumericKey(_key)) break;

                    key = KeyValidator.IsNumericKey(_key)
                        ? KeyValidator.GetDigitFromKey(_key).ToString()
                        : 0.ToString();

                    //Update byte
                    var byteValueCharArray_dec =
                        (byteOrder == ByteOrderType.HiLo)
                        ? BitConverter.ToUInt32(Byte.ToArray(), 0).ToString("d10").ToCharArray()
                        : BitConverter.ToUInt32(Enumerable.Reverse(Byte.ToArray()).ToArray(), 0).ToString("d10").ToCharArray();

                    var _newByte = new List<byte>();
                    uint result;
                    switch (_keyDownLabel)
                    {
                        case KeyDownLabel.FirstChar:
                            byteValueCharArray_dec[0] = key.ToCharArray()[0];
                            if (!uint.TryParse(new string(byteValueCharArray_dec), out result)) break;
                            _keyDownLabel = KeyDownLabel.SecondChar;
                            Action = ByteAction.Modified;
                            _newByte = new List<byte>(BitConverter.GetBytes(UInt32.Parse(new string(byteValueCharArray_dec))).Take(4).Reverse());

                            break;

                        case KeyDownLabel.SecondChar:
                            byteValueCharArray_dec[1] = key.ToCharArray()[0];
                            if (!uint.TryParse(new string(byteValueCharArray_dec), out result)) break;
                            _keyDownLabel = KeyDownLabel.ThirdChar;
                            Action = ByteAction.Modified;
                            _newByte = new List<byte>(BitConverter.GetBytes(UInt32.Parse(new string(byteValueCharArray_dec))).Take(4).Reverse());

                            break;

                        case KeyDownLabel.ThirdChar:
                            byteValueCharArray_dec[2] = key.ToCharArray()[0];
                            if (!uint.TryParse(new string(byteValueCharArray_dec), out result)) break;
                            _keyDownLabel = KeyDownLabel.FourthChar;
                            Action = ByteAction.Modified;
                            _newByte = new List<byte>(BitConverter.GetBytes(UInt32.Parse(new string(byteValueCharArray_dec))).Take(4).Reverse());

                            break;

                        case KeyDownLabel.FourthChar:
                            byteValueCharArray_dec[3] = key.ToCharArray()[0];
                            if (!uint.TryParse(new string(byteValueCharArray_dec), out result)) break;
                            _keyDownLabel = KeyDownLabel.FifthChar;
                            Action = ByteAction.Modified;
                            _newByte = new List<byte>(BitConverter.GetBytes(UInt32.Parse(new string(byteValueCharArray_dec))).Take(4).Reverse());

                            break;

                        case KeyDownLabel.FifthChar:
                            byteValueCharArray_dec[4] = key.ToCharArray()[0];
                            if (!uint.TryParse(new string(byteValueCharArray_dec), out result)) break;
                            _keyDownLabel = KeyDownLabel.SixthChar;
                            Action = ByteAction.Modified;
                            _newByte = new List<byte>(BitConverter.GetBytes(UInt32.Parse(new string(byteValueCharArray_dec))).Take(4).Reverse());

                            break;

                        case KeyDownLabel.SixthChar:
                            byteValueCharArray_dec[5] = key.ToCharArray()[0];
                            if (!uint.TryParse(new string(byteValueCharArray_dec), out result)) break;
                            _keyDownLabel = KeyDownLabel.SeventhChar;
                            Action = ByteAction.Modified;
                            _newByte = new List<byte>(BitConverter.GetBytes(UInt32.Parse(new string(byteValueCharArray_dec))).Take(4).Reverse());

                            break;

                        case KeyDownLabel.SeventhChar:
                            byteValueCharArray_dec[6] = key.ToCharArray()[0];
                            if (!uint.TryParse(new string(byteValueCharArray_dec), out result)) break;
                            _keyDownLabel = KeyDownLabel.EighthChar;
                            Action = ByteAction.Modified;
                            _newByte = new List<byte>(BitConverter.GetBytes(UInt32.Parse(new string(byteValueCharArray_dec))).Take(4).Reverse());

                            break;

                        case KeyDownLabel.EighthChar:
                            byteValueCharArray_dec[7] = key.ToCharArray()[0];
                            if (!uint.TryParse(new string(byteValueCharArray_dec), out result)) break;
                            _keyDownLabel = KeyDownLabel.Ninth;
                            Action = ByteAction.Modified;
                            _newByte = new List<byte>(BitConverter.GetBytes(UInt32.Parse(new string(byteValueCharArray_dec))).Take(4).Reverse());

                            break;

                        case KeyDownLabel.Ninth:
                            byteValueCharArray_dec[8] = key.ToCharArray()[0];
                            if (!uint.TryParse(new string(byteValueCharArray_dec), out result)) break;
                            _keyDownLabel = KeyDownLabel.Tenth;
                            Action = ByteAction.Modified;
                            _newByte = new List<byte>(BitConverter.GetBytes(UInt32.Parse(new string(byteValueCharArray_dec))).Take(4).Reverse());

                            break;

                        case KeyDownLabel.Tenth:
                            byteValueCharArray_dec[9] = key.ToCharArray()[0];
                            if (!uint.TryParse(new string(byteValueCharArray_dec), out result)) break;
                            _keyDownLabel = KeyDownLabel.NextPosition;
                            Action = ByteAction.Modified;
                            _newByte = new List<byte>(BitConverter.GetBytes(UInt32.Parse(new string(byteValueCharArray_dec))).Take(4).Reverse());

                            isLastChar = true;
                            break;
                        case KeyDownLabel.NextPosition:
                            break;
                    }

                    if (_newByte is not null && _newByte.Count == 4)
                    {
                        for (var i = 0; i < 4; i++)
                        {
                            if (byteOrder == ByteOrderType.LoHi)
                            {
                                if (Byte[i] != _newByte[i])
                                    ChangeByte(i, _newByte[i]);
                            }
                            else
                                if (Byte[3 - i] != _newByte[i])
                                ChangeByte(i, _newByte[i], ByteOrderType.HiLo);
                        }
                    }
                    #endregion

                    break;
                case DataVisualType.Binary:

                    #region Edit Binary value 

                    if (!KeyValidator.IsNumericKey(_key) || KeyValidator.GetDigitFromKey(_key) > 1) break;

                    key = KeyValidator.IsNumericKey(_key)
                        ? KeyValidator.GetDigitFromKey(_key).ToString()
                        : 0.ToString();

                    //Update byte
                    var byteValueCharArray_bin = (byteOrder == ByteOrderType.LoHi)
                        ? (Convert.ToString(Byte[0], 2).PadLeft(8, '0')
                        + Convert.ToString(Byte[1], 2).PadLeft(8, '0')
                        + Convert.ToString(Byte[2], 2).PadLeft(8, '0')
                        + Convert.ToString(Byte[3], 2).PadLeft(8, '0')).ToCharArray()
                        : (Convert.ToString(Byte[3], 2).PadLeft(8, '0')
                        + Convert.ToString(Byte[2], 2).PadLeft(8, '0')
                        + Convert.ToString(Byte[1], 2).PadLeft(8, '0')
                        + Convert.ToString(Byte[0], 2).PadLeft(8, '0')).ToCharArray();

                    switch (_keyDownLabel)
                    {
                        case KeyDownLabel.FirstChar:
                            byteValueCharArray_bin[0] = key.ToCharArray()[0];
                            _keyDownLabel = KeyDownLabel.SecondChar;
                            Action = ByteAction.Modified;
                            ChangeByte(0, Convert.ToByte(new string(byteValueCharArray_bin, 0, 8), 2), byteOrder: byteOrder);
                            break;

                        case KeyDownLabel.SecondChar:
                            byteValueCharArray_bin[1] = key.ToCharArray()[0];
                            _keyDownLabel = KeyDownLabel.ThirdChar;
                            Action = ByteAction.Modified;
                            ChangeByte(0, Convert.ToByte(new string(byteValueCharArray_bin, 0, 8), 2), byteOrder: byteOrder);
                            break;

                        case KeyDownLabel.ThirdChar:
                            byteValueCharArray_bin[2] = key.ToCharArray()[0];
                            _keyDownLabel = KeyDownLabel.FourthChar;
                            Action = ByteAction.Modified;
                            ChangeByte(0, Convert.ToByte(new string(byteValueCharArray_bin, 0, 8), 2), byteOrder: byteOrder);
                            break;

                        case KeyDownLabel.FourthChar:
                            byteValueCharArray_bin[3] = key.ToCharArray()[0];
                            _keyDownLabel = KeyDownLabel.FifthChar;
                            Action = ByteAction.Modified;
                            ChangeByte(0, Convert.ToByte(new string(byteValueCharArray_bin, 0, 8), 2), byteOrder: byteOrder);
                            break;

                        case KeyDownLabel.FifthChar:
                            byteValueCharArray_bin[4] = key.ToCharArray()[0];
                            _keyDownLabel = KeyDownLabel.SixthChar;
                            Action = ByteAction.Modified;
                            ChangeByte(0, Convert.ToByte(new string(byteValueCharArray_bin, 0, 8), 2), byteOrder: byteOrder);
                            break;

                        case KeyDownLabel.SixthChar:
                            byteValueCharArray_bin[5] = key.ToCharArray()[0];
                            _keyDownLabel = KeyDownLabel.SeventhChar;
                            Action = ByteAction.Modified;
                            ChangeByte(0, Convert.ToByte(new string(byteValueCharArray_bin, 0, 8), 2), byteOrder: byteOrder);
                            break;

                        case KeyDownLabel.SeventhChar:
                            byteValueCharArray_bin[6] = key.ToCharArray()[0];
                            _keyDownLabel = KeyDownLabel.EighthChar;
                            Action = ByteAction.Modified;
                            ChangeByte(0, Convert.ToByte(new string(byteValueCharArray_bin, 0, 8), 2), byteOrder: byteOrder);
                            break;

                        case KeyDownLabel.EighthChar:
                            byteValueCharArray_bin[7] = key.ToCharArray()[0];
                            _keyDownLabel = KeyDownLabel.Ninth;
                            Action = ByteAction.Modified;
                            ChangeByte(0, Convert.ToByte(new string(byteValueCharArray_bin, 0, 8), 2), byteOrder: byteOrder);
                            break;
                        case KeyDownLabel.Ninth:
                            byteValueCharArray_bin[8] = key.ToCharArray()[0];
                            _keyDownLabel = KeyDownLabel.Tenth;
                            Action = ByteAction.Modified;
                            ChangeByte(1, Convert.ToByte(new string(byteValueCharArray_bin, 8, 8), 2), byteOrder: byteOrder);
                            break;

                        case KeyDownLabel.Tenth:
                            byteValueCharArray_bin[9] = key.ToCharArray()[0];
                            _keyDownLabel = KeyDownLabel.Eleventh;
                            Action = ByteAction.Modified;
                            ChangeByte(1, Convert.ToByte(new string(byteValueCharArray_bin, 8, 8), 2), byteOrder: byteOrder);
                            break;

                        case KeyDownLabel.Eleventh:
                            byteValueCharArray_bin[10] = key.ToCharArray()[0];
                            _keyDownLabel = KeyDownLabel.Twelfth;
                            Action = ByteAction.Modified;
                            ChangeByte(1, Convert.ToByte(new string(byteValueCharArray_bin, 8, 8), 2), byteOrder: byteOrder);
                            break;

                        case KeyDownLabel.Twelfth:
                            byteValueCharArray_bin[11] = key.ToCharArray()[0];
                            _keyDownLabel = KeyDownLabel.Thirteenth;
                            Action = ByteAction.Modified;
                            ChangeByte(1, Convert.ToByte(new string(byteValueCharArray_bin, 8, 8), 2), byteOrder: byteOrder);
                            break;

                        case KeyDownLabel.Thirteenth:
                            byteValueCharArray_bin[12] = key.ToCharArray()[0];
                            _keyDownLabel = KeyDownLabel.Fourteenth;
                            Action = ByteAction.Modified;
                            ChangeByte(1, Convert.ToByte(new string(byteValueCharArray_bin, 8, 8), 2), byteOrder: byteOrder);
                            break;

                        case KeyDownLabel.Fourteenth:
                            byteValueCharArray_bin[13] = key.ToCharArray()[0];
                            _keyDownLabel = KeyDownLabel.Fifteenth;
                            Action = ByteAction.Modified;
                            ChangeByte(1, Convert.ToByte(new string(byteValueCharArray_bin, 8, 8), 2), byteOrder: byteOrder);
                            break;

                        case KeyDownLabel.Fifteenth:
                            byteValueCharArray_bin[14] = key.ToCharArray()[0];
                            _keyDownLabel = KeyDownLabel.Sixteenth;
                            Action = ByteAction.Modified;
                            ChangeByte(1, Convert.ToByte(new string(byteValueCharArray_bin, 8, 8), 2), byteOrder: byteOrder);
                            break;

                        case KeyDownLabel.Sixteenth:
                            byteValueCharArray_bin[15] = key.ToCharArray()[0];
                            _keyDownLabel = KeyDownLabel.Seventeenth;
                            Action = ByteAction.Modified;
                            ChangeByte(1, Convert.ToByte(new string(byteValueCharArray_bin, 8, 8), 2), byteOrder: byteOrder);
                            break;

                        case KeyDownLabel.Seventeenth:
                            byteValueCharArray_bin[16] = key.ToCharArray()[0];
                            _keyDownLabel = KeyDownLabel.Eighteenth;
                            Action = ByteAction.Modified;
                            ChangeByte(2, Convert.ToByte(new string(byteValueCharArray_bin, 16, 8), 2), byteOrder: byteOrder);
                            break;

                        case KeyDownLabel.Eighteenth:
                            byteValueCharArray_bin[17] = key.ToCharArray()[0];
                            _keyDownLabel = KeyDownLabel.Ninteenth;
                            Action = ByteAction.Modified;
                            ChangeByte(2, Convert.ToByte(new string(byteValueCharArray_bin, 16, 8), 2), byteOrder: byteOrder);
                            break;

                        case KeyDownLabel.Ninteenth:
                            byteValueCharArray_bin[18] = key.ToCharArray()[0];
                            _keyDownLabel = KeyDownLabel.Twentieth;
                            Action = ByteAction.Modified;
                            ChangeByte(2, Convert.ToByte(new string(byteValueCharArray_bin, 16, 8), 2), byteOrder: byteOrder);
                            break;

                        case KeyDownLabel.Twentieth:
                            byteValueCharArray_bin[19] = key.ToCharArray()[0];
                            _keyDownLabel = KeyDownLabel.TwentyFirst;
                            Action = ByteAction.Modified;
                            ChangeByte(2, Convert.ToByte(new string(byteValueCharArray_bin, 16, 8), 2), byteOrder: byteOrder);
                            break;

                        case KeyDownLabel.TwentyFirst:
                            byteValueCharArray_bin[20] = key.ToCharArray()[0];
                            _keyDownLabel = KeyDownLabel.TwentySecond;
                            Action = ByteAction.Modified;
                            ChangeByte(2, Convert.ToByte(new string(byteValueCharArray_bin, 16, 8), 2), byteOrder: byteOrder);
                            break;

                        case KeyDownLabel.TwentySecond:
                            byteValueCharArray_bin[21] = key.ToCharArray()[0];
                            _keyDownLabel = KeyDownLabel.TwentyThird;
                            Action = ByteAction.Modified;
                            ChangeByte(2, Convert.ToByte(new string(byteValueCharArray_bin, 16, 8), 2), byteOrder: byteOrder);
                            break;

                        case KeyDownLabel.TwentyThird:
                            byteValueCharArray_bin[22] = key.ToCharArray()[0];
                            _keyDownLabel = KeyDownLabel.TwentyFourth;
                            Action = ByteAction.Modified;
                            ChangeByte(2, Convert.ToByte(new string(byteValueCharArray_bin, 16, 8), 2), byteOrder: byteOrder);
                            break;

                        case KeyDownLabel.TwentyFourth:
                            byteValueCharArray_bin[23] = key.ToCharArray()[0];
                            _keyDownLabel = KeyDownLabel.TwentyFifth;
                            Action = ByteAction.Modified;
                            ChangeByte(2, Convert.ToByte(new string(byteValueCharArray_bin, 16, 8), 2), byteOrder: byteOrder);
                            break;

                        case KeyDownLabel.TwentyFifth:
                            byteValueCharArray_bin[24] = key.ToCharArray()[0];
                            _keyDownLabel = KeyDownLabel.TwentySixth;
                            Action = ByteAction.Modified;
                            ChangeByte(3, Convert.ToByte(new string(byteValueCharArray_bin, 24, 8), 2), byteOrder: byteOrder);
                            break;

                        case KeyDownLabel.TwentySixth:
                            byteValueCharArray_bin[25] = key.ToCharArray()[0];
                            _keyDownLabel = KeyDownLabel.TwentySeventh;
                            Action = ByteAction.Modified;
                            ChangeByte(3, Convert.ToByte(new string(byteValueCharArray_bin, 24, 8), 2), byteOrder: byteOrder);
                            break;

                        case KeyDownLabel.TwentySeventh:
                            byteValueCharArray_bin[26] = key.ToCharArray()[0];
                            _keyDownLabel = KeyDownLabel.TwentyEighth;
                            Action = ByteAction.Modified;
                            ChangeByte(3, Convert.ToByte(new string(byteValueCharArray_bin, 24, 8), 2), byteOrder: byteOrder);
                            break;

                        case KeyDownLabel.TwentyEighth:
                            byteValueCharArray_bin[27] = key.ToCharArray()[0];
                            _keyDownLabel = KeyDownLabel.TwentyNinth;
                            Action = ByteAction.Modified;
                            ChangeByte(3, Convert.ToByte(new string(byteValueCharArray_bin, 24, 8), 2), byteOrder: byteOrder);
                            break;

                        case KeyDownLabel.TwentyNinth:
                            byteValueCharArray_bin[28] = key.ToCharArray()[0];
                            _keyDownLabel = KeyDownLabel.Thirtieth;
                            Action = ByteAction.Modified;
                            ChangeByte(3, Convert.ToByte(new string(byteValueCharArray_bin, 24, 8), 2), byteOrder: byteOrder);
                            break;

                        case KeyDownLabel.Thirtieth:
                            byteValueCharArray_bin[29] = key.ToCharArray()[0];
                            _keyDownLabel = KeyDownLabel.ThirtyFirst;
                            Action = ByteAction.Modified;
                            ChangeByte(3, Convert.ToByte(new string(byteValueCharArray_bin, 24, 8), 2), byteOrder: byteOrder);
                            break;

                        case KeyDownLabel.ThirtyFirst:
                            byteValueCharArray_bin[30] = key.ToCharArray()[0];
                            _keyDownLabel = KeyDownLabel.ThirtySecond;
                            Action = ByteAction.Modified;
                            ChangeByte(3, Convert.ToByte(new string(byteValueCharArray_bin, 24, 8), 2), byteOrder: byteOrder);
                            break;

                        case KeyDownLabel.ThirtySecond:
                            byteValueCharArray_bin[31] = key.ToCharArray()[0];
                            _keyDownLabel = KeyDownLabel.NextPosition;
                            Action = ByteAction.Modified;
                            ChangeByte(3, Convert.ToByte(new string(byteValueCharArray_bin, 24, 8), 2), byteOrder: byteOrder);

                            isLastChar = true;
                            break;
                        case KeyDownLabel.NextPosition:
                            break;
                    }

                    #endregion

                    break;
            }
            return (Action, isLastChar);
        }

        public void ChangeByteValue(byte newValue, long position)
        {
            var index = (int)position % 4;

            Byte[index] = newValue;
            del_ByteOnChange?.Invoke(Byte, index);
        }
    }
}
