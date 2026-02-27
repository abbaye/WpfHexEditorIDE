/*
    Apache 2.0 2026
    ColorSpaceConverter - RGB ↔ HSV color space conversion utilities
    Author: Derek Tremblay (derektremblay666@gmail.com)
    Contributors: Claude Sonnet 4.5
*/

using System;

namespace WpfHexEditor.ColorPicker.Helpers
{
    /// <summary>
    /// Provides color space conversion methods for RGB and HSV color models.
    /// </summary>
    public static class ColorSpaceConverter
    {
        /// <summary>
        /// Converts RGB color values to HSV color space.
        /// </summary>
        /// <param name="r">Red component (0-255)</param>
        /// <param name="g">Green component (0-255)</param>
        /// <param name="b">Blue component (0-255)</param>
        /// <returns>Tuple containing (Hue: 0-360°, Saturation: 0-1, Value: 0-1)</returns>
        public static (double h, double s, double v) RgbToHsv(byte r, byte g, byte b)
        {
            // Normalize RGB values to 0-1 range
            double rf = r / 255.0;
            double gf = g / 255.0;
            double bf = b / 255.0;

            // Find min and max values
            double max = Math.Max(rf, Math.Max(gf, bf));
            double min = Math.Min(rf, Math.Min(gf, bf));
            double delta = max - min;

            // Calculate Hue (0-360°)
            double h = 0;
            if (delta != 0)
            {
                if (max == rf)
                {
                    h = 60 * (((gf - bf) / delta) % 6);
                }
                else if (max == gf)
                {
                    h = 60 * (((bf - rf) / delta) + 2);
                }
                else // max == bf
                {
                    h = 60 * (((rf - gf) / delta) + 4);
                }
            }

            // Ensure hue is positive
            if (h < 0)
                h += 360;

            // Calculate Saturation (0-1)
            double s = (max == 0) ? 0 : (delta / max);

            // Value is simply the max component (0-1)
            double v = max;

            return (h, s, v);
        }

        /// <summary>
        /// Converts HSV color values to RGB color space.
        /// </summary>
        /// <param name="h">Hue (0-360°)</param>
        /// <param name="s">Saturation (0-1)</param>
        /// <param name="v">Value (0-1)</param>
        /// <returns>Tuple containing (Red: 0-255, Green: 0-255, Blue: 0-255)</returns>
        public static (byte r, byte g, byte b) HsvToRgb(double h, double s, double v)
        {
            // Ensure hue is in valid range (0-360°)
            h = h % 360;
            if (h < 0)
                h += 360;

            // Clamp saturation and value to 0-1
            s = Math.Max(0.0, Math.Min(1.0, s));
            v = Math.Max(0.0, Math.Min(1.0, v));

            // Calculate chroma and intermediate values
            double c = v * s;
            double x = c * (1 - Math.Abs((h / 60) % 2 - 1));
            double m = v - c;

            double rf, gf, bf;

            // Determine RGB values based on hue sector
            if (h < 60)
            {
                rf = c; gf = x; bf = 0;
            }
            else if (h < 120)
            {
                rf = x; gf = c; bf = 0;
            }
            else if (h < 180)
            {
                rf = 0; gf = c; bf = x;
            }
            else if (h < 240)
            {
                rf = 0; gf = x; bf = c;
            }
            else if (h < 300)
            {
                rf = x; gf = 0; bf = c;
            }
            else
            {
                rf = c; gf = 0; bf = x;
            }

            // Add base value (m) and convert to 0-255 range
            return (
                (byte)Math.Round((rf + m) * 255),
                (byte)Math.Round((gf + m) * 255),
                (byte)Math.Round((bf + m) * 255)
            );
        }
    }
}
