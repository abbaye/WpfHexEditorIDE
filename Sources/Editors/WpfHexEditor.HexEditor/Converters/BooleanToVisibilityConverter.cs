// ==========================================================
// Project: WpfHexEditor.HexEditor
// File: BooleanToVisibilityConverter.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     Extended WPF IValueConverter that converts bool to Visibility with configurable
//     false-state values (Collapsed or Hidden). Inspired by NuGet Package Explorer.
//     Overcomes the limitation of the built-in WPF BooleanToVisibilityConverter.
//
// Architecture Notes:
//     Stateless converter with ConverterParameter support for false-value override.
//
// ==========================================================

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WpfHexEditor.HexEditor.Converters
{
    /// <summary>
    /// CODE FROM NUGET PACKAGE EXPLORER : https://github.com/NuGetPackageExplorer/NuGetPackageExplorer
    /// 
    /// This BooleanToVisibility converter allows us to override the converted value when
    /// the bound value is false.
    /// 
    /// The built-in converter in WPF restricts us to always use Collapsed when the bound 
    /// value is false.
    /// </summary>
    public sealed class BooleanToVisibilityConverter : IValueConverter
    {
        public bool Inverted { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var boolValue = (bool)value;

            if (Inverted)
                boolValue = !boolValue;

            return (string)parameter == "hidden"
                ? (boolValue ? Visibility.Visible : Visibility.Hidden)
                : (boolValue ? Visibility.Visible : Visibility.Collapsed);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}
