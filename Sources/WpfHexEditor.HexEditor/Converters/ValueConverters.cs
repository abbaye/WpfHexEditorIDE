//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using WpfHexEditor.Core;

namespace WpfHexEditor.HexEditor.Converters
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
}
