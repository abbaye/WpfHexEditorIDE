//////////////////////////////////////////////
// Apache 2.0  - 2020-2021
// Base author  : ehsan69h
// Modified by  : Abbaye
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using WpfHexaEditor.Core.Interfaces;
using System.Windows.Input;

namespace WpfHexaEditor.Core.Bytes
{
    class Byte_8bit : IByte
    {
        public Byte_8bit(byte value) => OriginByte = new List<byte> { value };

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
            byte value;
            var sign_positive = true;
            var prefix = "";

            switch (state)
            {
                case DataVisualState.Default:
                    value = Byte[0]; //[change]
                    break;
                case DataVisualState.Origin:
                    value = OriginByte[0]; //[change]
                    break;
                case DataVisualState.Changes:
                    if (Byte[0].CompareTo(OriginByte[0]) < 0) //[change]
                    {
                        sign_positive = false;
                        value = (byte)(OriginByte[0] - Byte[0]); //[change]
                    }
                    else
                        value = (byte)(Byte[0] - OriginByte[0]); //[change]

                    break;
                case DataVisualState.ChangesPercent:
                    prefix = "%";
                    if (Byte[0].CompareTo(OriginByte[0]) < 0)
                    {
                        sign_positive = false;
                        value = (byte)((OriginByte[0] - Byte[0]) * 100 / byte.MaxValue); //[change]
                    }
                    else
                        value = (byte)((Byte[0] - OriginByte[0]) * 100 / byte.MaxValue); //[change]

                    break;
                default:
                    goto case DataVisualState.Default;
            }

            if (state == DataVisualState.ChangesPercent)
                text = (sign_positive ? "" : "-") + prefix + value.ToString("d2");
            else
                switch (type)
                {
                    case DataVisualType.Hexadecimal:
                        var chArr = ByteConverters.ByteToHexCharArray(value);
                        text = (sign_positive ? "" : "-") + prefix +
                            new string(chArr);
                        break;
                    case DataVisualType.Decimal:
                        text = (sign_positive ? "" : "-") + prefix +
                            value.ToString("d3");
                        break;
                    case DataVisualType.Binary:
                        text = (sign_positive ? "" : "-") + prefix +
                            Convert.ToString(value, 2).PadLeft(8, '0');
                        break;
                }

            LongText = value;
            Text = text;

            return text;
        }

        public bool IsEqual(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return false;
            if (Byte == null || Byte.Count != 1) return false;
            if (bytes[0] == Byte[0]) return true;

            return false;
        }

        public void ChangeByte(int index, byte value)
        {
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
                    var byteValueCharArray = ByteConverters.ByteToHexCharArray(Byte[0]); //[change]
                    switch (_keyDownLabel)
                    {
                        case KeyDownLabel.FirstChar:
                            byteValueCharArray[0] = key.ToCharArray()[0];
                            _keyDownLabel = KeyDownLabel.SecondChar;
                            Action = ByteAction.Modified;
                            ChangeByte(0, ByteConverters.HexToByte(byteValueCharArray[0] + byteValueCharArray[1].ToString())[0]);
                            break;
                        case KeyDownLabel.SecondChar:
                            byteValueCharArray[1] = key.ToCharArray()[0];
                            _keyDownLabel = KeyDownLabel.NextPosition;

                            Action = ByteAction.Modified;
                            ChangeByte(0, ByteConverters.HexToByte(byteValueCharArray[0] + byteValueCharArray[1].ToString())[0]);
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
                    var byteValueCharArray_dec = Byte[0].ToString("d3").ToCharArray(); //[change]
                    switch (_keyDownLabel)
                    {
                        case KeyDownLabel.FirstChar:
                            byteValueCharArray_dec[0] = key.ToCharArray()[0];
                            if (int.Parse(new string(byteValueCharArray_dec)) > 255) break;
                            _keyDownLabel = KeyDownLabel.SecondChar;
                            Action = ByteAction.Modified;
                            Byte = new List<byte>() { BitConverter.GetBytes(int.Parse(new string(byteValueCharArray_dec)))[0] }; //[change]
                            break;

                        case KeyDownLabel.SecondChar:
                            byteValueCharArray_dec[1] = key.ToCharArray()[0];
                            if (int.Parse(new string(byteValueCharArray_dec)) > 255) break;
                            _keyDownLabel = KeyDownLabel.ThirdChar;
                            Action = ByteAction.Modified;
                            Byte = new List<byte>() { BitConverter.GetBytes(int.Parse(new string(byteValueCharArray_dec)))[0] }; //[change]
                            break;

                        case KeyDownLabel.ThirdChar:
                            byteValueCharArray_dec[2] = key.ToCharArray()[0];
                            if (int.Parse(new string(byteValueCharArray_dec)) > 255) break;
                            _keyDownLabel = KeyDownLabel.NextPosition;

                            Action = ByteAction.Modified;
                            Byte = new List<byte>() { BitConverter.GetBytes(int.Parse(new string(byteValueCharArray_dec)))[0] }; //[change]

                            isLastChar = true;
                            break;
                        case KeyDownLabel.NextPosition:
                            break;
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
                    var byteValueCharArray_bin = Convert
                        .ToString(Byte[0], 2) //[change]
                        .PadLeft(8, '0')
                        .ToCharArray();

                    switch (_keyDownLabel)
                    {
                        case KeyDownLabel.FirstChar:
                            byteValueCharArray_bin[0] = key.ToCharArray()[0];
                            _keyDownLabel = KeyDownLabel.SecondChar;
                            Action = ByteAction.Modified;
                            Byte = new List<byte>() { Convert.ToByte(new string(byteValueCharArray_bin), 2) }; //[change]
                            break;

                        case KeyDownLabel.SecondChar:
                            byteValueCharArray_bin[1] = key.ToCharArray()[0];
                            _keyDownLabel = KeyDownLabel.ThirdChar;
                            Action = ByteAction.Modified;
                            Byte = new List<byte>() { Convert.ToByte(new string(byteValueCharArray_bin), 2) }; //[change]
                            break;

                        case KeyDownLabel.ThirdChar:
                            byteValueCharArray_bin[2] = key.ToCharArray()[0];
                            _keyDownLabel = KeyDownLabel.FourthChar;
                            Action = ByteAction.Modified;
                            Byte = new List<byte>() { Convert.ToByte(new string(byteValueCharArray_bin), 2) }; //[change]
                            break;

                        case KeyDownLabel.FourthChar:
                            byteValueCharArray_bin[3] = key.ToCharArray()[0];
                            _keyDownLabel = KeyDownLabel.FifthChar;
                            Action = ByteAction.Modified;
                            Byte = new List<byte>() { Convert.ToByte(new string(byteValueCharArray_bin), 2) }; //[change]
                            break;

                        case KeyDownLabel.FifthChar:
                            byteValueCharArray_bin[4] = key.ToCharArray()[0];
                            _keyDownLabel = KeyDownLabel.SixthChar;
                            Action = ByteAction.Modified;
                            Byte = new List<byte>() { Convert.ToByte(new string(byteValueCharArray_bin), 2) }; //[change]
                            break;

                        case KeyDownLabel.SixthChar:
                            byteValueCharArray_bin[5] = key.ToCharArray()[0];
                            _keyDownLabel = KeyDownLabel.SeventhChar;
                            Action = ByteAction.Modified;
                            Byte = new List<byte>() { Convert.ToByte(new string(byteValueCharArray_bin), 2) }; //[change]
                            break;

                        case KeyDownLabel.SeventhChar:
                            byteValueCharArray_bin[6] = key.ToCharArray()[0];
                            _keyDownLabel = KeyDownLabel.EighthChar;
                            Action = ByteAction.Modified;
                            Byte = new List<byte>() { Convert.ToByte(new string(byteValueCharArray_bin), 2) }; //[change]
                            break;

                        case KeyDownLabel.EighthChar:
                            byteValueCharArray_bin[7] = key.ToCharArray()[0];
                            _keyDownLabel = KeyDownLabel.NextPosition;

                            Action = ByteAction.Modified;
                            Byte = new List<byte>() { Convert.ToByte(new string(byteValueCharArray_bin), 2) }; //[change]

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
            Byte[0] = newValue;
            del_ByteOnChange?.Invoke(Byte, 0);
        }
    }
}
