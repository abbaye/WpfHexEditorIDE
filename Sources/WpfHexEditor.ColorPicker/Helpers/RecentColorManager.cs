//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media;

namespace WpfHexEditor.ColorPicker.Helpers
{
    /// <summary>
    /// Manages a list of recently used colors with automatic file persistence.
    /// </summary>
    public static class RecentColorManager
    {
        private const int MaxRecentColors = 10;

        private static readonly string AppDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WpfHexaEditor");

        private static readonly string RecentColorsFile =
            Path.Combine(AppDataPath, "RecentColors.txt");

        /// <summary>
        /// Loads recent colors from disk. Returns default colors if file doesn't exist.
        /// </summary>
        /// <returns>List of recent colors (max 10)</returns>
        public static List<Color> LoadRecentColors()
        {
            var colors = new List<Color>();

            try
            {
                if (File.Exists(RecentColorsFile))
                {
                    foreach (var line in File.ReadAllLines(RecentColorsFile))
                    {
                        if (TryParseColor(line.Trim(), out Color color))
                        {
                            colors.Add(color);
                        }
                    }
                }
            }
            catch
            {
                // Silently fail - will return default colors
            }

            // If no colors loaded, return default palette
            if (colors.Count == 0)
            {
                colors.AddRange(new[]
                {
                    Colors.White,
                    Colors.Black,
                    Colors.Red,
                    Colors.Green,
                    Colors.Blue,
                    Colors.Yellow,
                    Colors.Cyan,
                    Colors.Magenta,
                    Colors.Orange,
                    Colors.Purple
                });
            }

            return colors.Take(MaxRecentColors).ToList();
        }

        /// <summary>
        /// Saves recent colors to disk.
        /// </summary>
        /// <param name="colors">List of colors to save (max 10 will be saved)</param>
        public static void SaveRecentColors(IEnumerable<Color> colors)
        {
            try
            {
                // Ensure directory exists
                Directory.CreateDirectory(AppDataPath);

                // Convert colors to hex strings (#AARRGGBB format)
                var lines = colors.Take(MaxRecentColors)
                    .Select(c => $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}");

                File.WriteAllLines(RecentColorsFile, lines);
            }
            catch
            {
                // Silently fail - not critical if save fails
            }
        }

        /// <summary>
        /// Adds a color to the recent colors list and saves to disk.
        /// Removes duplicates and maintains max size of 10.
        /// </summary>
        /// <param name="color">Color to add</param>
        /// <param name="currentColors">Current list of colors</param>
        /// <returns>Updated list with color added to front</returns>
        public static List<Color> AddRecentColor(Color color, List<Color> currentColors)
        {
            // Remove existing instances of this color
            currentColors.RemoveAll(c => c == color);

            // Add to front
            currentColors.Insert(0, color);

            // Limit to max size
            if (currentColors.Count > MaxRecentColors)
            {
                currentColors = currentColors.Take(MaxRecentColors).ToList();
            }

            // Save to disk
            SaveRecentColors(currentColors);

            return currentColors;
        }

        /// <summary>
        /// Tries to parse a hex color string into a Color object.
        /// Supports formats: #AARRGGBB, #RRGGBB, AARRGGBB, RRGGBB
        /// </summary>
        /// <param name="hex">Hex color string</param>
        /// <param name="color">Parsed color (output)</param>
        /// <returns>True if parsing succeeded</returns>
        private static bool TryParseColor(string hex, out Color color)
        {
            color = Colors.Transparent;
            hex = hex?.Trim().Replace("#", "").ToUpper() ?? "";

            try
            {
                if (hex.Length == 8) // AARRGGBB
                {
                    byte a = Convert.ToByte(hex.Substring(0, 2), 16);
                    byte r = Convert.ToByte(hex.Substring(2, 2), 16);
                    byte g = Convert.ToByte(hex.Substring(4, 2), 16);
                    byte b = Convert.ToByte(hex.Substring(6, 2), 16);
                    color = Color.FromArgb(a, r, g, b);
                    return true;
                }
                else if (hex.Length == 6) // RRGGBB (assume full opacity)
                {
                    byte r = Convert.ToByte(hex.Substring(0, 2), 16);
                    byte g = Convert.ToByte(hex.Substring(2, 2), 16);
                    byte b = Convert.ToByte(hex.Substring(4, 2), 16);
                    color = Color.FromArgb(255, r, g, b);
                    return true;
                }
            }
            catch
            {
                // Invalid hex format
            }

            return false;
        }
    }
}
