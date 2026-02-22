//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using WpfHexaEditor.Core;

namespace WpfHexaEditor.Converters
{
    /// <summary>
    /// Converts bool (IsSelected) to brush
    /// </summary>
    public class BoolToSelectionBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isSelected && isSelected)
                return new SolidColorBrush(Color.FromArgb(77, 51, 153, 255)); // #3399FF with alpha
            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts ByteAction to brush color using dynamic resources from UserControl
    /// </summary>
    public class ActionToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ByteAction action)
            {
                // Try to get the brush from the UserControl's resources (passed as parameter)
                // The parameter should be the UserControl itself
                if (parameter is FrameworkElement element && element != null)
                {
                    try
                    {
                        return action switch
                        {
                            ByteAction.Modified => element.TryFindResource("ModifiedBrush") as Brush ?? Brushes.Orange,
                            ByteAction.Added => element.TryFindResource("AddedBrush") as Brush ?? Brushes.LightGreen,
                            ByteAction.Deleted => element.TryFindResource("DeletedBrush") as Brush ?? Brushes.Red,
                            _ => Brushes.Transparent
                        };
                    }
                    catch
                    {
                        // Fallback to hardcoded colors
                    }
                }

                // Fallback to hardcoded colors if resources not available
                return action switch
                {
                    ByteAction.Modified => Brushes.Orange,
                    ByteAction.Added => Brushes.LightGreen,
                    ByteAction.Deleted => Brushes.Red,
                    _ => Brushes.Transparent
                };
            }
            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts bool (isValid) to Border background color for validation indicators
    /// </summary>
    public class BoolToValidationColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isValid)
            {
                return new SolidColorBrush(isValid ? Color.FromRgb(76, 175, 80) : Colors.Transparent); // Green #4CAF50
            }
            return new SolidColorBrush(Colors.Transparent);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts bool (isValid) to icon text (✓ when valid, empty when invalid)
    /// </summary>
    public class BoolToValidationIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isValid)
            {
                return isValid ? "✓" : "";
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    #region ColorPicker and HexEditorSettings Converters

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

    /// <summary>
    /// Converter for Enum to ComboBox SelectedIndex
    /// Maps enum value to ComboBoxItem Tag string comparison
    /// </summary>
    public class EnumToComboBoxConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Return the enum value as string for comparison with ComboBoxItem Tag
            if (value != null)
            {
                return value.ToString();
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Convert Tag string back to enum
            if (value is string strValue && !string.IsNullOrEmpty(strValue) && targetType.IsEnum)
            {
                try
                {
                    return Enum.Parse(targetType, strValue);
                }
                catch
                {
                    return Binding.DoNothing;
                }
            }
            return Binding.DoNothing;
        }
    }

    /// <summary>
    /// Converter for BytePerLine property to ComboBox SelectedIndex
    /// Maps 8->0, 16->1, 24->2, 32->3
    /// </summary>
    public class BytesPerLineToIndexConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int bytesPerLine)
            {
                return bytesPerLine switch
                {
                    8 => 0,
                    16 => 1,
                    24 => 2,
                    32 => 3,
                    _ => 1 // Default to 16 bytes
                };
            }
            return 1; // Default to 16 bytes
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int index)
            {
                return index switch
                {
                    0 => 8,
                    1 => 16,
                    2 => 24,
                    3 => 32,
                    _ => 16 // Default to 16 bytes
                };
            }
            return 16; // Default to 16 bytes
        }
    }

    /// <summary>
    /// Converter for double zoom value to percentage string (e.g., 1.0 -> "100%")
    /// </summary>
    public class ZoomToPercentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double zoom)
            {
                return $"{zoom * 100:0}%";
            }
            return "100%";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    #endregion
}
