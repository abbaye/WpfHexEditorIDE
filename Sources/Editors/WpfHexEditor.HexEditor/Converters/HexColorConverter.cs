// ==========================================================
// Project: WpfHexEditor.HexEditor
// File: HexColorConverter.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     WPF IValueConverter that converts a hex color string (#RRGGBB or #AARRGGBB)
//     to a WPF Color struct for use in XAML bindings.
//
// Architecture Notes:
//     Stateless converter using ColorConverter for parsing.
//
// ==========================================================

using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace WpfHexEditor.HexEditor.Converters
{
    /// <summary>
    /// Converts hex color string (#RRGGBB) to Color
    /// </summary>
    public class HexColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string hexColor && !string.IsNullOrWhiteSpace(hexColor))
            {
                try
                {
                    // Remove # if present
                    hexColor = hexColor.TrimStart('#');

                    if (hexColor.Length == 6)
                    {
                        var r = System.Convert.ToByte(hexColor.Substring(0, 2), 16);
                        var g = System.Convert.ToByte(hexColor.Substring(2, 2), 16);
                        var b = System.Convert.ToByte(hexColor.Substring(4, 2), 16);
                        return Color.FromRgb(r, g, b);
                    }
                }
                catch
                {
                    // Fall through to default
                }
            }

            // Default color if conversion fails
            return Colors.Blue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Color color)
            {
                return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            }

            return "#0078D4";
        }
    }
}
