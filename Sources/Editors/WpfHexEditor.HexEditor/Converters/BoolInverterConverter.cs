// ==========================================================
// Project: WpfHexEditor.HexEditor
// File: BoolInverterConverter.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     WPF IValueConverter that inverts a boolean value.
//     Returns the negated bool for use in XAML bindings where a negated
//     condition is required (e.g., IsEnabled="{Binding IsReadOnly, Converter=...}").
//
// Architecture Notes:
//     Stateless sealed converter. Handles nullable bool gracefully.
//
// ==========================================================

using System;
using System.Globalization;
using System.Windows.Data;

namespace WpfHexEditor.HexEditor.Converters
{
    /// <summary>
    /// Invert bool
    /// </summary>
    public sealed class BoolInverterConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool? val = null;

            try
            {
                val = value is not null && (bool)value;
            }
            catch
            {
                // ignored
            }

            return !val;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}
