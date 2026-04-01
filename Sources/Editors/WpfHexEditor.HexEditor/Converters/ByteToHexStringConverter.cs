// ==========================================================
// Project: WpfHexEditor.HexEditor
// File: ByteToHexStringConverter.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     WPF IValueConverter that converts a byte value to a hexadecimal string
//     representation (e.g., 0xFF). Supports optional 0x prefix tag via Show0xTag property.
//
// Architecture Notes:
//     Stateless sealed converter. Relies on WpfHexEditor.Core byte utilities.
//
// ==========================================================

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
