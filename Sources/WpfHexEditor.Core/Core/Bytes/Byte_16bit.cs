//////////////////////////////////////////////
// Apache 2.0  - 2020-2021
// Base author  : ehsan69h
// Modified by  : Abbaye
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using WpfHexEditor.Core.Interfaces;

namespace WpfHexEditor.Core.Bytes
{
    class Byte_16bit : IByte
    {
        public Byte_16bit(byte[] value) => OriginByte = new List<byte>(value);

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
            var value = new byte[2];
            var sign_positive = true;
            var prefix = "";
            var byteValue = (order == ByteOrderType.HiLo) ? Byte.ToArray().AsEnumerable().Reverse().ToArray() : Byte.ToArray();
            var originValue = (order == ByteOrderType.HiLo) ? OriginByte.ToArray().AsEnumerable().Reverse().ToArray() : OriginByte.ToArray();
            var ByteInt = BitConverter.ToUInt16(byteValue.AsEnumerable().Reverse().ToArray(), 0);
            var OriginInt = BitConverter.ToUInt16(originValue.AsEnumerable().Reverse().ToArray(), 0);

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
                        value = BitConverter.GetBytes(OriginInt - ByteInt).Take(2).Reverse().ToArray();
                    }
                    else
                        value = BitConverter.GetBytes(ByteInt - OriginInt).Take(2).Reverse().ToArray();

                    break;
                case DataVisualState.ChangesPercent:
                    prefix = "%";
                    if (ByteInt.CompareTo(OriginInt) < 0)
                    {
                        sign_positive = false;
                        value = BitConverter.GetBytes((OriginInt - ByteInt) * 100 / ushort.MaxValue);
                    }
                    else
                        value = BitConverter.GetBytes((ByteInt - OriginInt) * 100 / ushort.MaxValue);

                    break;
                default:
                    goto case DataVisualState.Default;
            }

            if (state == DataVisualState.ChangesPercent)
                text = (sign_positive ? "" : "-") + prefix + BitConverter.ToUInt16(value, 0).ToString("d2");
            else
            {
                switch (type)
                {
                    case DataVisualType.Hexadecimal:
                        var chArr = ByteConverters.ByteToHexCharArray(value[0]).Concat(ByteConverters.ByteToHexCharArray(value[1])).ToArray();
                        text = (sign_positive ? "" : "-") + prefix + new string(chArr);
                        break;
                    case DataVisualType.Decimal:
                        text = (sign_positive ? "" : "-") + prefix +
                            BitConverter.ToUInt16(value: value.AsEnumerable().Reverse().ToArray(), startIndex: 0).ToString("d5");
                        break;
                    case DataVisualType.Binary:
                        text = (sign_positive ? "" : "-") + prefix +
                            Convert.ToString(value[0], 2).PadLeft(8, '0') +
                            Convert.ToString(value[1], 2).PadLeft(8, '0');
                        break;
                }
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
                index = 1 - index;
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
                        ? ByteConverters.ByteToHexCharArray(Byte[0]).Concat(ByteConverters.ByteToHexCharArray(Byte[1])).ToArray()
                            : ByteConverters.ByteToHexCharArray(Byte[1]).Concat(ByteConverters.ByteToHexCharArray(Byte[0])).ToArray();

                    switch (_keyDownLabel)
                    {
                        case KeyDownLabel.FirstChar:
                            byteValueCharArray[0] = key.ToCharArray()[0];
                            _keyDownLabel = KeyDownLabel.SecondChar;
                            Action = ByteAction.Modified;
                            ChangeByte(0, ByteConverters.HexToByte(byteValueCharArray[0] + byteValueCharArray[1].ToString())[0]
                                , byteOrder: byteOrder);
                            break;

                        case KeyDownLabel.SecondChar:
                            byteValueCharArray[1] = key.ToCharArray()[0];
                            _keyDownLabel = KeyDownLabel.ThirdChar;

                            Action = ByteAction.Modified;
                            ChangeByte(0, ByteConverters.HexToByte(byteValueCharArray[0] + byteValueCharArray[1].ToString())[0]
                                , byteOrder: byteOrder);
                            break;
                        case KeyDownLabel.ThirdChar:
                            byteValueCharArray[2] = key.ToCharArray()[0];
                            _keyDownLabel = KeyDownLabel.FourthChar;
                            Action = ByteAction.Modified;
                            ChangeByte(1, ByteConverters.HexToByte(byteValueCharArray[2] + byteValueCharArray[3].ToString())[0]
                                , byteOrder: byteOrder);
                            break;
                        case KeyDownLabel.FourthChar:
                            byteValueCharArray[3] = key.ToCharArray()[0];
                            _keyDownLabel = KeyDownLabel.NextPosition;

                            Action = ByteAction.Modified;
                            ChangeByte(1, ByteConverters.HexToByte(byteValueCharArray[2] + byteValueCharArray[3].ToString())[0]
                                , byteOrder: byteOrder);
                            isLastChar = true;
                            break;
                        case KeyDownLabel.NextPosition:
                            break;
                    }

                    #endregion

                    break;
                case DataVisualType.Decimal:

                    #region Edit decimal value 

                    if (!KeyValidator.IsNumericKey(_key))
                    {
                        break;
                    }
                    key = KeyValidator.IsNumericKey(_key)
                        ? KeyValidator.GetDigitFromKey(_key).ToString()
                        : 0.ToString();

                    //Update byte
                    var byteValueCharArray_dec =
                        (byteOrder == ByteOrderType.HiLo)
                            ? BitConverter.ToUInt16(Byte.ToArray(), 0).ToString("d5").ToCharArray()
                        : BitConverter.ToUInt16(Enumerable.Reverse(Byte.ToArray()).ToArray(), 0).ToString("d5").ToCharArray();

                    var _newByte = new List<byte>();
                    switch (_keyDownLabel)
                    {
                        case KeyDownLabel.FirstChar:
                            byteValueCharArray_dec[0] = key.ToCharArray()[0];
                            if (uint.Parse(new string(byteValueCharArray_dec)) > uint.MaxValue) break;
                            _keyDownLabel = KeyDownLabel.SecondChar;
                            Action = ByteAction.Modified;
                            _newByte = new List<byte>(BitConverter.GetBytes(uint.Parse(new string(byteValueCharArray_dec))).Take(2).Reverse());

                            break;

                        case KeyDownLabel.SecondChar:
                            byteValueCharArray_dec[1] = key.ToCharArray()[0];
                            if (uint.Parse(new string(byteValueCharArray_dec)) > uint.MaxValue) break;
                            _keyDownLabel = KeyDownLabel.ThirdChar;
                            Action = ByteAction.Modified;
                            _newByte = new List<byte>(BitConverter.GetBytes(uint.Parse(new string(byteValueCharArray_dec))).Take(2).Reverse());
                            break;

                        case KeyDownLabel.ThirdChar:
                            byteValueCharArray_dec[2] = key.ToCharArray()[0];
                            if (uint.Parse(new string(byteValueCharArray_dec)) > uint.MaxValue) break;
                            _keyDownLabel = KeyDownLabel.FourthChar;
                            Action = ByteAction.Modified;
                            _newByte = new List<byte>(BitConverter.GetBytes(uint.Parse(new string(byteValueCharArray_dec))).Take(2).Reverse());

                            break;

                        case KeyDownLabel.FourthChar:
                            byteValueCharArray_dec[3] = key.ToCharArray()[0];
                            if (uint.Parse(new string(byteValueCharArray_dec)) > uint.MaxValue) break;
                            _keyDownLabel = KeyDownLabel.FifthChar;
                            Action = ByteAction.Modified;
                            _newByte = new List<byte>(BitConverter.GetBytes(uint.Parse(new string(byteValueCharArray_dec))).Take(2).Reverse());

                            break;

                        case KeyDownLabel.FifthChar:
                            byteValueCharArray_dec[4] = key.ToCharArray()[0];
                            if (uint.Parse(new string(byteValueCharArray_dec)) > uint.MaxValue) break;
                            _keyDownLabel = KeyDownLabel.NextPosition;
                            Action = ByteAction.Modified;
                            _newByte = new List<byte>(BitConverter.GetBytes(uint.Parse(new string(byteValueCharArray_dec))).Take(2).Reverse());

                            isLastChar = true;
                            break;
                        case KeyDownLabel.NextPosition:
                            break;
                    }
                    for (var i = 0; i < 2; i++)
                    {
                        if (byteOrder == ByteOrderType.LoHi)
                        {
                            if (Byte[i] != _newByte[i])
                                ChangeByte(i, _newByte[i]);
                        }
                        else
                            if (Byte[1 - i] != _newByte[i])
                            ChangeByte(i, _newByte[i], ByteOrderType.HiLo);
                    }
                    #endregion

                    break;
                case DataVisualType.Binary:

                    #region Edit Binary value 

                    if (!KeyValidator.IsNumericKey(_key)
                        || KeyValidator.GetDigitFromKey(_key) > 1)
                    {
                        break;
                    }
                    key = KeyValidator.IsNumericKey(_key)
                        ? KeyValidator.GetDigitFromKey(_key).ToString()
                        : 0.ToString();

                    //Update byte
                    var byteValueCharArray_bin = (byteOrder == ByteOrderType.LoHi)
                        ? (Convert.ToString(Byte[0], 2).PadLeft(8, '0') + Convert.ToString(Byte[1], 2).PadLeft(8, '0')).ToCharArray()
                            : (Convert.ToString(Byte[1], 2).PadLeft(8, '0') + Convert.ToString(Byte[0], 2).PadLeft(8, '0')).ToCharArray();

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
                            _keyDownLabel = KeyDownLabel.NextPosition;
                            Action = ByteAction.Modified;
                            ChangeByte(1, Convert.ToByte(new string(byteValueCharArray_bin, 8, 8), 2), byteOrder: byteOrder);
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
            if (position % 2 == 0)
            {
                Byte[0] = newValue;
                del_ByteOnChange?.Invoke(Byte, 0);
            }
            else
            {
                Byte[1] = newValue;
                del_ByteOnChange?.Invoke(Byte, 1);
            }
        }
    }
}
