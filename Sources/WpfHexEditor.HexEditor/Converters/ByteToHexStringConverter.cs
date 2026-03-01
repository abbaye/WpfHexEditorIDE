//////////////////////////////////////////////
// Apache 2.0  - 2021
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Globalization;
using System.Windows.Data;
using WpfHexEditor.Core;

namespace WpfHexEditor.HexEditor.Converters
{
    /// <summary>
    /// Used to convert byte value to hexadecimal string like this 0xFF.
    /// </summary>
    public sealed class ByteToHexStringConverter : IValueConverter
    {
        public bool Show0xTag { get; set; } = true;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is not null
                ? (byte.TryParse(value.ToString(), out var byteValue)
                    ? (byteValue >= 0
                        ? (Show0xTag ? "0x" : "") + byteValue
                              .ToString(ConstantReadOnly.Hex2StringFormat, CultureInfo.InvariantCulture)
                              .ToUpperInvariant()
                        : ConstantReadOnly.DefaultHex2String)
                    : ConstantReadOnly.DefaultHex2String)
                : ConstantReadOnly.DefaultHex2String;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value;
    }
}
