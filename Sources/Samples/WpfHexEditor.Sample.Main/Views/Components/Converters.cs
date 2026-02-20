//////////////////////////////////////////////
// Apache 2.0  2026
// HexEditor V2 - Shared Converters
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WpfHexEditor.Sample.Main.Views.Components
{
    /// <summary>
    /// Converter for boolean to visibility
    /// </summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                return visibility == Visibility.Visible;
            }
            return false;
        }
    }

    /// <summary>
    /// Converter for visibility to boolean (reverse of BoolToVisibilityConverter)
    /// </summary>
    public class VisibilityToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                return visibility == Visibility.Visible;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }
    }

    /// <summary>
    /// Converter for integer to boolean (used for BytesPerLine radio buttons)
    /// </summary>
    public class IntToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int intValue && parameter is string strParam && int.TryParse(strParam, out int paramValue))
            {
                return intValue == paramValue;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue && boolValue && parameter is string strParam && int.TryParse(strParam, out int paramValue))
            {
                return paramValue;
            }
            return Binding.DoNothing;
        }
    }

    /// <summary>
    /// Converter for theme selection (compares theme name with selected theme)
    /// </summary>
    public class ThemeConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length == 2 && values[0] != null && values[1] != null)
            {
                return values[0].ToString() == values[1].ToString();
            }
            return false;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
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
