//////////////////////////////////////////////
// Apache 2.0  - 2026
// Color converters for WpfHexEditor.ColorPicker
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace WpfHexEditor.ColorPicker.Converters
{
    /// <summary>
    /// Converter for Color to SolidColorBrush
    /// Used for binding Color properties to Brush-based WPF properties
    /// </summary>
    public class ColorToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Color color)
            {
                return new SolidColorBrush(color);
            }
            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SolidColorBrush brush)
            {
                return brush.Color;
            }
            return Colors.Transparent;
        }
    }

    /// <summary>
    /// Converter for Color to contrasting text brush (Black or White)
    /// Automatically chooses black text for light colors, white text for dark colors
    /// </summary>
    public class ColorToContrastBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Color color)
            {
                // Calculate perceived brightness using standard luminance formula
                double brightness = (color.R * 299 + color.G * 587 + color.B * 114) / 1000.0;
                return brightness > 128 ? Brushes.Black : Brushes.White;
            }
            return Brushes.Black;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converter that creates a rainbow gradient brush for hue slider
    /// Returns a vertical gradient from Red → Yellow → Green → Cyan → Blue → Magenta → Red
    /// </summary>
    public class HueToGradientBrushConverter : IValueConverter
    {
        private static LinearGradientBrush _cachedBrush;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Cache the gradient brush since it's always the same
            if (_cachedBrush == null)
            {
                _cachedBrush = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 1),  // Start from bottom
                    EndPoint = new Point(0, 0)     // End at top
                };

                _cachedBrush.GradientStops.Add(new GradientStop(Color.FromRgb(255, 0, 0), 0.0));      // Red (0°)
                _cachedBrush.GradientStops.Add(new GradientStop(Color.FromRgb(255, 255, 0), 0.17));   // Yellow (60°)
                _cachedBrush.GradientStops.Add(new GradientStop(Color.FromRgb(0, 255, 0), 0.33));     // Green (120°)
                _cachedBrush.GradientStops.Add(new GradientStop(Color.FromRgb(0, 255, 255), 0.5));    // Cyan (180°)
                _cachedBrush.GradientStops.Add(new GradientStop(Color.FromRgb(0, 0, 255), 0.67));     // Blue (240°)
                _cachedBrush.GradientStops.Add(new GradientStop(Color.FromRgb(255, 0, 255), 0.83));   // Magenta (300°)
                _cachedBrush.GradientStops.Add(new GradientStop(Color.FromRgb(255, 0, 0), 1.0));      // Red (360°)
            }

            return _cachedBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
