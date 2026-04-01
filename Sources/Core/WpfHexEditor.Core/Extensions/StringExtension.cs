// ==========================================================
// Project: WpfHexEditor.Core
// File: StringExtension.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     Extension methods for measuring WPF string rendering dimensions, enabling
//     pixel-accurate width calculation of characters in a given font for the
//     hex editor's fixed-width column layout calculations.
//
// Architecture Notes:
//     Adapted from a StackOverflow solution (2012). Contains WPF dependencies
//     (FormattedText, Typeface). Used by HexEditor for dynamic column sizing
//     based on the configured FontFamily and FontSize.
//
// ==========================================================

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WpfHexEditor.Core.Extensions
{
    public static class StringExtension
    {
        /// <summary>
        /// Get the screen size of a string
        /// </summary>
        public static Size GetScreenSize(this string text, FontFamily fontFamily, double fontSize, FontStyle fontStyle,
            FontWeight fontWeight, FontStretch fontStretch, Brush foreGround, Visual visual)
        {
            fontFamily ??= new TextBlock().FontFamily;
            fontSize = fontSize > 0 ? fontSize : new TextBlock().FontSize;

            var ft = new FormattedText(text ?? string.Empty,
                                       CultureInfo.InvariantCulture,
                                       FlowDirection.LeftToRight,
                                       new Typeface(fontFamily, fontStyle, fontWeight, fontStretch),
                                       fontSize,
                                       foreGround,
                                       VisualTreeHelper.GetDpi(visual).PixelsPerDip);

            return new Size(ft.Width, ft.Height);
        }
    }
}
