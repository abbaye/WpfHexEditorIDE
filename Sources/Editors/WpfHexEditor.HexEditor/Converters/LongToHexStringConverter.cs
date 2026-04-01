// ==========================================================
// Project: WpfHexEditor.HexEditor
// File: LongToHexStringConverter.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     WPF IValueConverter that converts a long (Int64) value to a hexadecimal
//     string representation (e.g., 0xFFFFFFFF) for display in offset and position fields.
//
// Architecture Notes:
//     Stateless sealed converter using WpfHexEditor.Core byte utilities.
//
// ==========================================================

using System;
using System.Globalization;
using System.Windows.Data;
using WpfHexEditor.Core;

namespace WpfHexEditor.HexEditor.Converters
{
    /// <summary>
    /// Used to convert long value to hexadecimal string like this 0xFFFFFFFF.
    /// </summary>
    public sealed class LongToHexStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is not null
                ? (long.TryParse(value.ToString(), out var longValue)
                    ? (longValue > -1
                        ? "0x" + longValue
                              .ToString(ConstantReadOnly.HexLineInfoStringFormat, CultureInfo.InvariantCulture)
                              .ToUpperInvariant()
                        : ConstantReadOnly.DefaultHex8String)
                    : ConstantReadOnly.DefaultHex8String)
                : ConstantReadOnly.DefaultHex8String;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value;
    }
}
