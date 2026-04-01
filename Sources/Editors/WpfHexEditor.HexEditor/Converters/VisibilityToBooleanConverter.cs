// ==========================================================
// Project: WpfHexEditor.HexEditor
// File: VisibilityToBooleanConverter.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     WPF IValueConverter that converts between Visibility and bool in both directions.
//     Allows two-way binding between visibility state and boolean properties.
//
// Architecture Notes:
//     Stateless sealed converter supporting both Convert and ConvertBack directions.
//
// ==========================================================

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WpfHexEditor.HexEditor.Converters
{
    /// <summary>
    /// This VisibilityToBoolean converter convert Visibility <-> Boolean
    /// </summary>
    public sealed class VisibilityToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            (Visibility)value == Visibility.Visible;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            (bool)value == true
                ? Visibility.Visible
                : Visibility.Collapsed;
    }
}
