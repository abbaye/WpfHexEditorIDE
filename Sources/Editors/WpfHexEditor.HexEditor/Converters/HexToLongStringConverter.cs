// ==========================================================
// Project: WpfHexEditor.HexEditor
// File: HexToLongStringConverter.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     WPF IValueConverter that converts a hexadecimal string representation
//     to a long (Int64) value for binding numeric inputs in hex format.
//
// Architecture Notes:
//     Stateless sealed converter using WpfHexEditor.Core.Bytes utilities.
//
// ==========================================================

using System;
using System.Globalization;
using System.Windows.Data;
using WpfHexEditor.Core.Bytes;

namespace WpfHexEditor.HexEditor.Converters
{
    /// <summary>
    /// Used to convert hexadecimal to Long value.
    /// </summary>
    public sealed class HexToLongStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is null) return string.Empty;

            var (success, val) = ByteConverters.IsHexValue(value.ToString());

            return success
                ? (object)val
                : string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value;
    }
}
