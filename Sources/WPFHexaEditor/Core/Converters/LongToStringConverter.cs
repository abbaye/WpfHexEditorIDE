// Apache 2.0 - 2026
// Author: Claude Sonnet 4.5
// Contributors: Derek Tremblay (derektremblay666@gmail.com)

using System;
using System.Globalization;
using System.Windows.Data;

namespace WpfHexaEditor.Core.Converters
{
    /// <summary>
    /// Converts between long and string for TextBox bindings.
    /// Used for properties like ByteShiftLeft.
    /// </summary>
    [ValueConversion(typeof(long), typeof(string))]
    public class LongToStringConverter : IValueConverter
    {
        /// <summary>
        /// Convert long to string for display in TextBox
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is long longValue)
            {
                return longValue.ToString();
            }
            return "0";
        }

        /// <summary>
        /// Convert string from TextBox back to long
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string stringValue)
            {
                // Empty string = 0
                if (string.IsNullOrWhiteSpace(stringValue))
                    return 0L;

                // Try to parse as decimal
                if (long.TryParse(stringValue, NumberStyles.Integer, culture, out long result))
                {
                    return result;
                }

                // Try to parse as hex (0x prefix)
                if (stringValue.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ||
                    stringValue.StartsWith("0X", StringComparison.OrdinalIgnoreCase))
                {
                    string hexValue = stringValue.Substring(2);
                    if (long.TryParse(hexValue, NumberStyles.HexNumber, culture, out result))
                    {
                        return result;
                    }
                }
            }

            // Return DependencyProperty.UnsetValue to indicate binding failure
            // This prevents invalid values from being set
            return System.Windows.DependencyProperty.UnsetValue;
        }
    }
}
