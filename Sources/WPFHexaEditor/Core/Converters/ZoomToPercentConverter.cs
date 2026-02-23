// Apache 2.0 - 2026
// Author: Claude Sonnet 4.5
// Contributors: Derek Tremblay (derektremblay666@gmail.com)

using System;
using System.Globalization;
using System.Windows.Data;

namespace WpfHexaEditor.Core.Converters
{
    /// <summary>
    /// Converts between zoom scale (double 0.5-2.0) and percentage string ("50%" - "200%").
    /// Used for ZoomScale property display in sliders.
    /// </summary>
    [ValueConversion(typeof(double), typeof(string))]
    public class ZoomToPercentConverter : IValueConverter
    {
        /// <summary>
        /// Convert zoom scale (0.5-2.0) to percentage string ("50%" - "200%")
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double doubleValue)
            {
                int percentage = (int)(doubleValue * 100);
                return $"{percentage}%";
            }
            return "100%";
        }

        /// <summary>
        /// Convert percentage string back to zoom scale double
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string stringValue)
            {
                // Remove % sign if present
                stringValue = stringValue.Replace("%", "").Trim();

                if (double.TryParse(stringValue, NumberStyles.Float, culture, out double percentage))
                {
                    // Convert percentage to scale (100% = 1.0)
                    double scale = percentage / 100.0;

                    // Clamp to valid range (0.5 - 2.0)
                    scale = Math.Max(0.5, Math.Min(2.0, scale));
                    return scale;
                }
            }

            // Return DependencyProperty.UnsetValue to indicate binding failure
            return System.Windows.DependencyProperty.UnsetValue;
        }
    }
}
